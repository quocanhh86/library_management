using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using web_mvc_management.Common;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models;

namespace web_mvc_management.Services;

/// <summary>
/// Dịch vụ gửi email xác thực chuẩn an ninh:
/// - SEC-17: Cooldown Engine 60 giây chống Mail Toll Fraud & Spam DoS.
/// - Tích hợp Gmail SMTP bảo mật với TLS (Port 587) qua IOptions&lt;EmailSettings&gt;.
/// - Fallback thông minh ghi log console trong môi trường dev nếu mất kết nối mạng.
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
            logger.LogInformation("[DEV OTP] Mã OTP cho {Email} [{Purpose}]: {Code}", normalizedEmail, purpose, otpCode);
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(_settings.Username) && !string.IsNullOrWhiteSpace(_settings.Password))
            {
                using var client = new SmtpClient(_settings.Host, _settings.Port)
                {
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
                logger.LogInformation("Đã gửi email OTP thành công tới: {Email} cho mục đích: {Purpose}", normalizedEmail, purpose);
            }
            else
            {
                logger.LogWarning("Chưa cấu hình tài khoản SMTP, giả lập gửi OTP tới {Email}: {Code}", normalizedEmail, otpCode);
            }

            // Cập nhật mốc thời gian Cooldown
            CooldownTracker[normalizedEmail] = dateTimeProvider.UtcNow;
            return ServiceResult.Success("Đã gửi mã mới tới email của bạn.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi gửi email OTP tới {Email} qua SMTP server {Host}:{Port}", normalizedEmail, _settings.Host, _settings.Port);

            // Trong môi trường dev, nếu gửi mail thất bại (do mạng/tường lửa), ghi log rõ OTP để lập trình viên test
            if (env.IsDevelopment())
            {
                logger.LogWarning("[DEV FALLBACK] Mã OTP cho {Email} [{Purpose}]: {Code}", normalizedEmail, purpose, otpCode);
                CooldownTracker[normalizedEmail] = dateTimeProvider.UtcNow;
                return ServiceResult.Success("Đã gửi mã mới (môi trường Development: xem mã trong log của server).");
            }

            return ServiceResult.Failure("Chưa gửi được email vì không kết nối được máy chủ thư. Thử lại sau ít phút.");
        }
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
