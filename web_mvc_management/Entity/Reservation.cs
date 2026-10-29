using web_mvc_management.Constant;

namespace web_mvc_management.Entity;

/// <summary>
/// Đặt trước sách (bảng reservations).
/// </summary>
public class Reservation : BaseEntity
{
    public Guid ReaderId { get; set; }

    public Guid BookId { get; set; }

    public string ReservationStatus { get; set; } = LibraryConstants.ReservationStatus.Pending;

    public DateTimeOffset ReservedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? AssignedCopyId { get; set; }

    public DateTimeOffset? ReadyAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    // Navigation properties
    public virtual Reader Reader { get; set; } = null!;
    public virtual Book Book { get; set; } = null!;
    public virtual BookCopy? AssignedCopy { get; set; }
}

