using web_mvc_management.Constant;

namespace web_mvc_management.Entity;

/// <summary>
/// Bản sao từng cuốn sách / mã vạch cụ thể (bảng book_copies).
/// </summary>
public class BookCopy : BaseEntity
{
    public Guid BookId { get; set; }

    public Guid? ShelfId { get; set; }

    public string Barcode { get; set; } = string.Empty;

    public string CopyStatus { get; set; } = LibraryConstants.CopyStatus.Available;

    public decimal? Price { get; set; }

    public DateOnly AcquiredDate { get; set; }

    public DateTimeOffset? DisposedAt { get; set; }

    public string? DisposalReason { get; set; }

    public string? Note { get; set; }

    // Navigation properties
    public virtual Book Book { get; set; } = null!;
    public virtual Shelf? Shelf { get; set; }
    public virtual ICollection<BorrowDetail> BorrowDetails { get; set; } = [];
    public virtual ICollection<Reservation> Reservations { get; set; } = [];
}

