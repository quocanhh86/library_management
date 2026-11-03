using web_mvc_management.Common;

namespace web_mvc_management.Entity;

/// <summary>
/// Thực thể lưu trữ mã OTP trong CSDL phục vụ xác thực an toàn (bảng otps).
/// Đáp ứng các tiêu chuẩn an ninh: SEC-04 (Burn OTP), SEC-08 (Persistent DB), SEC-12 (Purpose Isolation).
/// </summary>
public class OtpEntry : IEntity<Guid>
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Email { get; set; } = string.Empty;

    public string OtpCode { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public int FailedAttempts { get; set; } = 0;

    public bool IsUsed { get; set; } = false;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
