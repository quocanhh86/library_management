using System.Security.Claims;
using web_mvc_management.Common;
using web_mvc_management.Entity;
using web_mvc_management.Models.ViewModels.Auth;

namespace web_mvc_management.Services;

public class LoginStepResult
{
    public bool RequiresOtp { get; set; } = true;
    public string Email { get; set; } = string.Empty;
    public string MaskedEmail { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}

public interface IAuthService
{
    /// <summary>
    /// Gửi OTP xác thực đăng ký độc giả sau khi validate dữ liệu và kiểm tra trùng lặp.
    /// </summary>
    Task<ServiceResult> SendRegisterOtpAsync(ReaderRegisterViewModel model, string clientIp, CancellationToken ct = default);

    /// <summary>
    /// Hoàn tất đăng ký độc giả sau khi xác thực OTP thành công, cấp mã thẻ thư viện.
    /// </summary>
    Task<ServiceResult<Reader>> CompleteRegistrationAsync(ReaderRegisterViewModel model, string otpCode, string clientIp, CancellationToken ct = default);

    /// <summary>
    /// Bước 1 đăng nhập độc giả: kiểm tra tài khoản, mật khẩu (PBKDF2 và Timing Attack Defense),
    /// kiểm tra lockout và gửi OTP 2FA.
    /// </summary>
    Task<ServiceResult<LoginStepResult>> InitiateReaderLoginAsync(ReaderLoginViewModel model, string clientIp, CancellationToken ct = default);

    /// <summary>
    /// Bước 2 đăng nhập độc giả: xác thực OTP 2FA, reset bộ đếm lỗi và trả về ClaimsPrincipal.
    /// </summary>
    Task<ServiceResult<ClaimsPrincipal>> CompleteReaderLoginOtpAsync(string email, string otpCode, string clientIp, CancellationToken ct = default);

    /// <summary>
    /// Bước 1 đăng nhập cán bộ thư viện (Admin / Librarian): kiểm tra Gmail, bảo vệ chống dò tài khoản (SEC-09)
    /// và gửi OTP qua Gmail.
    /// </summary>
    Task<ServiceResult<LoginStepResult>> InitiateStaffLoginAsync(StaffLoginViewModel model, string clientIp, CancellationToken ct = default);

    /// <summary>
    /// Bước 2 đăng nhập cán bộ thư viện: xác thực OTP từ Gmail, gán Role Admin/Librarian và trả về ClaimsPrincipal.
    /// </summary>
    Task<ServiceResult<ClaimsPrincipal>> CompleteStaffLoginOtpAsync(string email, string otpCode, string clientIp, CancellationToken ct = default);

    /// <summary>
    /// Gửi lại mã OTP cho một email và purpose với kiểm tra Cooldown 60s và IP Rate Limit.
    /// </summary>
    Task<ServiceResult> ResendOtpAsync(string email, string purpose, string clientIp, CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách các loại độc giả đang hoạt động để phục vụ form đăng ký mở thẻ.
    /// </summary>
    Task<IReadOnlyList<ReaderType>> GetActiveReaderTypesAsync(CancellationToken ct = default);
}
