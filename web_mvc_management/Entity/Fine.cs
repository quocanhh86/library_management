using web_mvc_management.Constant;

namespace web_mvc_management.Entity;

/// <summary>
/// Phiếu phạt vi phạm (bảng fines: quá hạn, làm mất, làm hỏng).
/// </summary>
public class Fine : BaseEntity
{
    public Guid BorrowDetailId { get; set; }

    public string FineType { get; set; } = LibraryConstants.FineType.Overdue;

    public int? OverdueDays { get; set; }

    public decimal Amount { get; set; }

    public string FineStatus { get; set; } = LibraryConstants.FineStatus.Unpaid;

    public DateTimeOffset? PaidAt { get; set; }

    public string? PaymentMethod { get; set; }

    public Guid? HandledBy { get; set; }

    public string? Note { get; set; }

    // Navigation properties
    public virtual BorrowDetail BorrowDetail { get; set; } = null!;
    public virtual Staff? HandledByStaff { get; set; }
}

