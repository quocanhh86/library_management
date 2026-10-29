namespace web_mvc_management.Entity;

/// <summary>
/// Cấu hình quy định thư viện (bảng library_settings).
/// </summary>
public class LibrarySetting : BaseEntity
{
    public decimal OverdueFinePerDay { get; set; }

    public short DamagedFinePercent { get; set; }

    public short LostFinePercent { get; set; }

    public decimal MaxUnpaidFine { get; set; }

    public short ReservationHoldHours { get; set; }
}

