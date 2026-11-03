using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using web_mvc_management.Common;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models;

namespace web_mvc_management.Services;

/// <summary>
/// Dịch vụ gửi email xác thực chuẩn an ninh:
/// - SEC-17: Cooldown Engine 60 giây chống Mail Toll Fraud và Spam DoS.
/// - Tích hợp Gmail SMTP bảo mật với TLS (Port 587) qua IOptions&lt;EmailSettings&gt;.
/// - Chẩn đoán chi tiết mã phản hồi SMTP (535 5.7.8 BadCredentials, v.v.) và Fallback thông minh ghi log console trong môi trường dev.
/// </summary>
public class EmailService(
    IOptions<EmailSettings> emailOptions,
    IDateTimeProvider dateTimeProvider,
    ILogger<EmailService> logger,
    IHostEnvironment env) : IEmailService
{
    public const int CooldownSeconds = 60;
    private static readonly ConcurrentDictionary<string, DateTimeOffset> CooldownTracker = new();

    private readonly EmailSettings _settings = emailOptions.Value;

    public int GetRemainingCooldownSeconds(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return 0;
        }

        var key = email.Trim().ToLowerInvariant();
        if (CooldownTracker.TryGetValue(key, out var lastSent))
        {
            var elapsed = dateTimeProvider.UtcNow - lastSent;
            var remaining = CooldownSeconds - (int)elapsed.TotalSeconds;
            return remaining > 0 ? remaining : 0;
        }

        return 0;
    }

    public async Task<ServiceResult> SendOtpEmailAsync(
        string toEmail,
        string otpCode,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(otpCode);

        var normalizedEmail = toEmail.Trim().ToLowerInvariant();

        // SEC-17: Kiểm tra Cooldown 60s chống Mail Toll Fraud
        var remainingSeconds = GetRemainingCooldownSeconds(normalizedEmail);
        if (remainingSeconds > 0)
        {
            return ServiceResult.Failure(
                $"Yêu cầu gửi mã quá nhanh. Vui lòng chờ {remainingSeconds} giây nữa trước khi gửi lại.");
        }

        var (subject, purposeTitle) = ResolvePurposeInfo(purpose);
        var bodyHtml = GenerateEmailBody(otpCode, purposeTitle);

        if (env.IsDevelopment())
        {
            logger.LogInformation("[DEV OTP] Mã OTP khởi tạo cho {Email} [{Purpose}]: {Code}", normalizedEmail, purpose, otpCode);
        }

        try
        {
            if (_settings.IsConfigured)
            {
                using var client = new SmtpClient(_settings.Host, _settings.Port)
                {
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                    EnableSsl = _settings.StartTls,
                    Timeout = 10000
                };

                using var message = new MailMessage
                {
                    From = new MailAddress(_settings.Username, _settings.SenderDisplayName),
                    Subject = subject,
                    Body = bodyHtml,
                    IsBodyHtml = true
                };
                message.To.Add(normalizedEmail);

                await client.SendMailAsync(message, cancellationToken);
                logger.LogInformation(
                    "[GMAIL SMTP OK] Đã gửi email OTP thành công từ {Sender} tới {Email} [{Purpose}] qua {Host}:{Port}",
                    _settings.Username, normalizedEmail, purpose, _settings.Host, _settings.Port);
            }
            else
            {
                logger.LogWarning(
                    "========================================================================\n" +
                    "[SMTP CHƯA CẤU HÌNH] MailSettings:Username/Password đang trống.\n" +
                    "[DEV OTP] Người nhận: {Email} | Mục đích: {Purpose} | MÃ OTP: {Code}\n" +
                    "========================================================================",
                    normalizedEmail, purpose, otpCode);
            }

            // Cập nhật mốc thời gian Cooldown
            CooldownTracker[normalizedEmail] = dateTimeProvider.UtcNow;
            return ServiceResult.Success("Đã gửi mã mới tới email của bạn.");
        }
        catch (Exception ex)
        {
            var smtpDiag = await DiagnoseSmtpFailureAsync(ex, cancellationToken);

            // Trong môi trường dev, ghi log rõ nguyên nhân SMTP + khung mã OTP nổi bật ở cuối log để lập trình viên lấy mã ngay
            if (env.IsDevelopment())
            {
                logger.LogWarning(
                    "\n========================================================================\n" +
                    "[CHẨN ĐOÁN GMAIL SMTP - KHÔNG GỬI ĐƯỢC THƯ RA NGOÀI]\n" +
                    "- Máy chủ SMTP : {Host}:{Port} (TLS/STARTTLS: {StartTls})\n" +
                    "- Tài khoản gửi: {Sender} (Độ dài App Password: {PassLen} ký tự)\n" +
                    "- Chi tiết lỗi : {SmtpDiag}\n" +
                    "------------------------------------------------------------------------\n" +
                    "[DEV FALLBACK OTP] Dùng mã dưới đây để nhập trên trình duyệt:\n" +
                    "  + Người nhận : {Email}\n" +
                    "  + Mục đích   : {Purpose}\n" +
                    "  + MÃ OTP     : {Code} (Hiệu lực: 5 phút)\n" +
                    "========================================================================",
                    _settings.Host,
                    _settings.Port,
                    _settings.StartTls,
                    _settings.Username,
                    _settings.Password?.Length ?? 0,
                    smtpDiag,
                    normalizedEmail,
                    purpose,
                    otpCode);

                CooldownTracker[normalizedEmail] = dateTimeProvider.UtcNow;
                return ServiceResult.Success("Đã sinh mã OTP (Gmail SMTP từ chối xác thực tài khoản gửi - Môi trường Development: xem mã OTP trong cửa sổ Log của Server).");
            }

            logger.LogError(ex, "Lỗi gửi email OTP tới {Email} qua {Host}:{Port}: {Diag}", normalizedEmail, _settings.Host, _settings.Port, smtpDiag);
            return ServiceResult.Failure("Chưa gửi được email vì không kết nối được máy chủ thư. Thử lại sau ít phút.");
        }
    }

    /// <summary>
    /// Kiểm tra nhanh phản hồi thực tế từ máy chủ SMTP khi SmtpClient đóng kết nối đột ngột (ví dụ lỗi 535-5.7.8 BadCredentials của Gmail).
    /// </summary>
    private async Task<string> DiagnoseSmtpFailureAsync(Exception originalException, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(_settings.Host, _settings.Port, cts.Token);
            await using var netStream = tcp.GetStream();
            using var reader = new StreamReader(netStream, Encoding.ASCII, leaveOpen: true);
            await using var writer = new StreamWriter(netStream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true };

            _ = await reader.ReadLineAsync(cts.Token); // 220 banner
            await writer.WriteLineAsync("EHLO localhost");
            while (await reader.ReadLineAsync(cts.Token) is { } ehloLine)
            {
                if (ehloLine.Length >= 4 && ehloLine[3] == ' ') break;
            }

            if (_settings.StartTls)
            {
                await writer.WriteLineAsync("STARTTLS");
                var startTlsResp = await reader.ReadLineAsync(cts.Token);
                if (startTlsResp == null || !startTlsResp.StartsWith("220", StringComparison.Ordinal))
                {
                    return $"Máy chủ không chấp nhận STARTTLS: {startTlsResp}";
                }

                await using var ssl = new SslStream(netStream, leaveInnerStreamOpen: true);
                await ssl.AuthenticateAsClientAsync(_settings.Host);
                using var sslReader = new StreamReader(ssl, Encoding.ASCII, leaveOpen: true);
                await using var sslWriter = new StreamWriter(ssl, Encoding.ASCII, leaveOpen: true) { AutoFlush = true };

                await sslWriter.WriteLineAsync("EHLO localhost");
                while (await sslReader.ReadLineAsync(cts.Token) is { } tlsEhloLine)
                {
                    if (tlsEhloLine.Length >= 4 && tlsEhloLine[3] == ' ') break;
                }

                await sslWriter.WriteLineAsync("AUTH LOGIN");
                _ = await sslReader.ReadLineAsync(cts.Token); // 334 Username

                await sslWriter.WriteLineAsync(Convert.ToBase64String(Encoding.ASCII.GetBytes(_settings.Username)));
                _ = await sslReader.ReadLineAsync(cts.Token); // 334 Password

                await sslWriter.WriteLineAsync(Convert.ToBase64String(Encoding.ASCII.GetBytes(_settings.Password)));
                var authResp = await sslReader.ReadLineAsync(cts.Token);
                if (!string.IsNullOrWhiteSpace(authResp) && authResp.StartsWith("535", StringComparison.Ordinal))
                {
                    return $"{authResp} -> Tài khoản Gmail '{_settings.Username}' hoặc App Password đã bị Google từ chối/thu hồi (cần tạo lại App Password 16 ký tự mới trong Google Account Security).";
                }

                return authResp ?? originalException.Message;
            }
        }
        catch
        {
            // Bỏ qua nếu probe phụ gặp lỗi, trả về thông điệp gốc
        }

        return originalException.InnerException?.Message ?? originalException.Message;
    }

    private static (string Subject, string Title) ResolvePurposeInfo(string purpose) => purpose switch
    {
        "REGISTER" => ("[Thư Viện UTC] Mã OTP kích hoạt tài khoản độc giả", "Xác Thực Đăng Ký Tài Khoản"),
        "READER_LOGIN" => ("[Thư Viện UTC] Mã OTP đăng nhập 2 bước độc giả", "Xác Thực Đăng Nhập Độc Giả (2FA)"),
        "STAFF_LOGIN" => ("[Thư Viện UTC] Mã OTP đăng nhập cán bộ thư viện", "Xác Thực Đăng Nhập Cán Bộ / Thủ Thư"),
        "RESET_PASSWORD" => ("[Thư Viện UTC] Mã OTP đặt lại mật khẩu", "Đặt Lại Mật Khẩu"),
        _ => ("[Thư Viện UTC] Mã xác thực OTP", "Xác Thực Hệ Thống")
    };

    private static string GenerateEmailBody(string otpCode, string purposeTitle)
    {
        return $$"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8">
            <style>
                body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f8fafc; margin: 0; padding: 24px; }
                .container { max-width: 540px; margin: 0 auto; background: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); }
                .header { background: #0d6efd; color: #ffffff; padding: 28px 24px; text-align: center; }
                .header h1 { margin: 0; font-size: 22px; font-weight: 700; }
                .header p { margin: 6px 0 0 0; opacity: 0.9; font-size: 14px; }
                .content { padding: 32px 28px; text-align: center; }
                .otp-box { background: #f1f5f9; border: 2px dashed #0d6efd; border-radius: 10px; padding: 18px 24px; margin: 24px auto; display: inline-block; }
                .otp-code { font-size: 34px; font-weight: 800; letter-spacing: 8px; color: #0d6efd; font-family: monospace; }
                .warning { color: #dc3545; font-size: 13px; margin-top: 20px; font-weight: 600; }
                .footer { background: #f8fafc; padding: 18px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #e2e8f0; }
            </style>
        </head>
        <body>
            <div class="container">
                <div class="header">
                    <h1>Hệ Thống Thư Viện UTC</h1>
                    <p>{{purposeTitle}}</p>
                </div>
                <div class="content">
                    <p style="font-size: 15px; color: #334155; margin-top: 0;">Chào bạn, dưới đây là mã xác thực OTP dùng một lần của bạn:</p>
                    <div class="otp-box">
                        <div class="otp-code">{{otpCode}}</div>
                    </div>
                    <p style="font-size: 14px; color: #64748b; margin-bottom: 0;">Mã có hiệu lực trong vòng <strong>5 phút</strong>.</p>
                    <p class="warning">⚠️ Tuyệt đối KHÔNG chia sẻ mã này cho bất kỳ ai để bảo vệ an toàn tài khoản.</p>
                </div>
                <div class="footer">
                    &copy; 2026 Trường Đại học Giao thông vận tải (UTC) &bull; Thư viện điện tử
                </div>
            </div>
        </body>
        </html>
        """;
    }
}
