namespace web_mvc_management.Entity;

/// <summary>
/// Phiếu mượn sách (bảng borrow_slips).
/// </summary>
public class BorrowSlip : BaseEntity
{
    public Guid ReaderId { get; set; }

    public Guid IssuedBy { get; set; }

    public DateTimeOffset BorrowedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? Note { get; set; }

    // Navigation properties
    public virtual Reader Reader { get; set; } = null!;
    public virtual Staff IssuedByStaff { get; set; } = null!;
    public virtual ICollection<BorrowDetail> BorrowDetails { get; set; } = [];
}

