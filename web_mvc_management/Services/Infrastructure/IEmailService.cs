using web_mvc_management.Common;

namespace web_mvc_management.Services;

public interface IEmailService
{
    /// <summary>
    /// Gửi mã xác thực OTP qua Email với cơ chế kiểm tra Cooldown 60 giây (SEC-17).
    /// </summary>
    Task<ServiceResult> SendOtpEmailAsync(string toEmail, string otpCode, string purpose, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra thời gian còn lại (giây) của Cooldown gửi OTP cho một địa chỉ email.
    /// Trả về 0 nếu đã hết thời gian chờ và có thể gửi ngay.
    /// </summary>
    int GetRemainingCooldownSeconds(string email);
}
