namespace web_mvc_management.Entity;

/// <summary>
/// Chi tiết từng cuốn sách trong phiếu mượn (bảng borrow_details).
/// </summary>
public class BorrowDetail : BaseEntity
{
    public Guid SlipId { get; set; }

    public Guid CopyId { get; set; }

    public DateOnly DueDate { get; set; }

    public short RenewCount { get; set; } = 0;

    public DateTimeOffset? ReturnedAt { get; set; }

    public string? ReturnCondition { get; set; }

    public Guid? ReceivedBy { get; set; }

    public string? ReturnNote { get; set; }

    // Navigation properties
    public virtual BorrowSlip BorrowSlip { get; set; } = null!;
    public virtual BookCopy BookCopy { get; set; } = null!;
    public virtual Staff? ReceivedByStaff { get; set; }
    public virtual ICollection<Fine> Fines { get; set; } = [];
}

