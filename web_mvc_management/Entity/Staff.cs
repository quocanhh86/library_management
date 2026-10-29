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

    public DateTimeOffset? LastLoginAt { get; set; }

    // Navigation properties
    public virtual ICollection<BorrowSlip> IssuedBorrowSlips { get; set; } = [];
    public virtual ICollection<BorrowDetail> ReceivedBorrowDetails { get; set; } = [];
    public virtual ICollection<Fine> HandledFines { get; set; } = [];
}

