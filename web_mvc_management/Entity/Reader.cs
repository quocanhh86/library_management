namespace web_mvc_management.Entity;

/// <summary>
/// Độc giả thư viện (bảng readers).
/// </summary>
public class Reader : BaseEntity
{
    public Guid ReaderTypeId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public string CardNo { get; set; } = string.Empty;

    public string? MemberCode { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Address { get; set; }

    public DateOnly CardIssuedDate { get; set; }

    public DateOnly CardExpiryDate { get; set; }

    public string CardStatus { get; set; } = string.Empty;

    public DateTimeOffset? LockedAt { get; set; }

    public string? LockReason { get; set; }

    public int FailedLoginAttempts { get; set; } = 0;

    public DateTimeOffset? LastLoginAttempt { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();

    // Navigation properties
    public virtual ReaderType ReaderType { get; set; } = null!;
    public virtual ICollection<BorrowSlip> BorrowSlips { get; set; } = [];
    public virtual ICollection<Reservation> Reservations { get; set; } = [];
}
