using System;
using System.Threading;
using System.Threading.Tasks;
using web_mvc_management.Constant;

namespace web_mvc_management.Services;

public enum OtpStatus
{
    Success,
    Incorrect,
    Burned,
    Expired,
    NotFound
}

public class OtpVerificationResult
{
    public OtpStatus Status { get; init; }
    public bool IsSuccess => Status == OtpStatus.Success;
    public bool IsBurned => Status == OtpStatus.Burned;
    public int RemainingAttempts { get; init; }
    public string Message { get; init; } = string.Empty;

    public static OtpVerificationResult Success(string message = "Xác thực mã OTP thành công.") =>
        new() { Status = OtpStatus.Success, Message = message, RemainingAttempts = 0 };

    public static OtpVerificationResult Incorrect(int remainingAttempts, string message) =>
        new() { Status = OtpStatus.Incorrect, RemainingAttempts = remainingAttempts, Message = message };

    public static OtpVerificationResult Burned(string? message = null) =>
        new() { Status = OtpStatus.Burned, RemainingAttempts = 0, Message = message ?? ErrorMessages.Format(ErrorCode.OtpBurned, 5) };

    public static OtpVerificationResult Expired(string? message = null) =>
        new() { Status = OtpStatus.Expired, RemainingAttempts = 0, Message = message ?? ErrorMessages.Get(ErrorCode.OtpExpired) };

    public static OtpVerificationResult NotFound(string? message = null) =>
        new() { Status = OtpStatus.NotFound, RemainingAttempts = 0, Message = message ?? ErrorMessages.Get(ErrorCode.OtpNotFound) };
}

/// <summary>
/// Dịch vụ quản lý mã xác thực OTP an toàn (SEC-04, SEC-08, SEC-12, SEC-14).
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Sinh mã OTP 6 chữ số ngẫu nhiên an toàn, lưu bền vững vào CSDL và vô hiệu hóa mã cũ cùng mục đích.
    /// </summary>
    Task<string> GenerateOtpAsync(string email, string purpose, TimeSpan? validity = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra mã OTP với cơ chế đếm số lần sai và tự động đốt hủy (Burn OTP) ở lần sai thứ 5.
    /// </summary>
    Task<OtpVerificationResult> VerifyOtpAsync(string email, string purpose, string otpCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Thu hồi/hủy mã OTP hiện tại của một email và mục đích.
    /// </summary>
    Task InvalidateOtpAsync(string email, string purpose, CancellationToken cancellationToken = default);
}
