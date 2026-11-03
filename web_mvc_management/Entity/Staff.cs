using web_mvc_management.Constant;

namespace web_mvc_management.Entity;

/// <summary>
/// Nhân viên thư viện / Quản trị viên (bảng staff).
/// </summary>
public class Staff : BaseEntity
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string Role { get; set; } = AppRoles.Librarian;

    public int FailedLoginAttempts { get; set; } = 0;

    public DateTimeOffset? LastLoginAttempt { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();

    public DateTimeOffset? LastLoginAt { get; set; }

    // Navigation properties
    public virtual ICollection<BorrowSlip> IssuedBorrowSlips { get; set; } = [];
    public virtual ICollection<BorrowDetail> ReceivedBorrowDetails { get; set; } = [];
    public virtual ICollection<Fine> HandledFines { get; set; } = [];
}
