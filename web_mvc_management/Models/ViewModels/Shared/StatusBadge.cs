using web_mvc_management.Constant;

namespace web_mvc_management.Models.ViewModels.Shared;

public enum StatusKind
{
    Copy,
    Card,
    Loan,
    Fine,
    Reservation,
    Record
}

/// <summary>
/// Model cho partial _StatusBadge. Mọi badge trạng thái trong hệ thống đi qua đây để màu, chữ, icon thống nhất.
/// </summary>
public record StatusBadge(StatusKind Kind, string Code)
{
    /// <summary>Trạng thái suy ra, không có trong DB: thẻ còn ≤ 30 ngày.</summary>
    public const string CardExpiringSoon = "EXPIRING";

    /// <summary>Trạng thái hiển thị của phiếu mượn (tính từ hạn trả, không lưu DB).</summary>
    public static class Loan
    {
        public const string Active = "ACTIVE";
        public const string DueSoon = "DUE_SOON";
        public const string Overdue = "OVERDUE";
        public const string Returned = "RETURNED";
    }

    public (string Label, string Tone, string? Icon) Resolve() => (Kind, Code) switch
    {
        (StatusKind.Record, "ACTIVE") => ("Hoạt động", "ok", "check-circle"),
        (StatusKind.Record, "INACTIVE") => ("Ngừng hoạt động", "neutral", null),
        (StatusKind.Record, "LOCKED") => ("Tạm khóa", "stamp", "lock"),
        (StatusKind.Copy, LibraryConstants.CopyStatus.Available) => ("Sẵn sàng", "ok", "check-circle"),
        (StatusKind.Copy, LibraryConstants.CopyStatus.Borrowed) => ("Đang mượn", "brand", null),
        (StatusKind.Copy, LibraryConstants.CopyStatus.Reserved) => ("Đang giữ chỗ", "brand", null),
        (StatusKind.Copy, LibraryConstants.CopyStatus.Damaged) => ("Hỏng", "warn", "exclamation-triangle"),
        (StatusKind.Copy, LibraryConstants.CopyStatus.Lost) => ("Mất", "stamp", "x-octagon"),
        (StatusKind.Copy, LibraryConstants.CopyStatus.Disposed) => ("Đã thanh lý", "neutral", null),

        (StatusKind.Card, LibraryConstants.CardStatus.Active) => ("Còn hạn", "ok", null),
        (StatusKind.Card, CardExpiringSoon) => ("Sắp hết hạn", "warn", "clock"),
        (StatusKind.Card, LibraryConstants.CardStatus.Expired) => ("Hết hạn", "stamp", "exclamation-octagon"),
        (StatusKind.Card, LibraryConstants.CardStatus.Locked) => ("Bị khóa", "stamp", "lock"),

        (StatusKind.Loan, Loan.Active) => ("Đang mượn", "brand", null),
        (StatusKind.Loan, Loan.DueSoon) => ("Sắp đến hạn", "warn", "clock"),
        (StatusKind.Loan, Loan.Overdue) => ("Quá hạn", "stamp", "exclamation-octagon"),
        (StatusKind.Loan, Loan.Returned) => ("Đã trả", "ok", "check-circle"),

        (StatusKind.Fine, LibraryConstants.FineStatus.Unpaid) => ("Chưa nộp", "stamp", null),
        (StatusKind.Fine, LibraryConstants.FineStatus.Paid) => ("Đã nộp", "ok", "check-circle"),
        (StatusKind.Fine, LibraryConstants.FineStatus.Waived) => ("Được miễn", "neutral", null),
        (StatusKind.Fine, LibraryConstants.FineStatus.Cancelled) => ("Đã hủy", "neutral", null),

        (StatusKind.Reservation, LibraryConstants.ReservationStatus.Pending) => ("Đang chờ", "neutral", "hourglass-split"),
        (StatusKind.Reservation, LibraryConstants.ReservationStatus.Ready) => ("Chờ nhận sách", "ok", "check-circle"),
        (StatusKind.Reservation, LibraryConstants.ReservationStatus.Fulfilled) => ("Đã nhận", "brand", null),
        (StatusKind.Reservation, LibraryConstants.ReservationStatus.Cancelled) => ("Đã hủy", "neutral", null),
        (StatusKind.Reservation, LibraryConstants.ReservationStatus.Expired) => ("Hết hạn giữ", "stamp", "clock"),

        _ => (Code, "neutral", null)
    };
}

/// <summary>
/// Model cho partial _DueStamp: con dấu hạn trả. Chỉ dùng cho hạn trả.
/// </summary>
public record DueStamp(DateOnly DueDate, DateOnly Today, bool Returned = false)
{
    /// <summary>TODO(xác nhận): ngưỡng "sắp đến hạn".</summary>
    public const int DueSoonDays = 3;

    public int DaysLeft => DueDate.DayNumber - Today.DayNumber;

    public string State => Returned ? "done" : DaysLeft < 0 ? "late" : DaysLeft <= DueSoonDays ? "soon" : "ok";

    public string Note => State switch
    {
        "done" => "Đã trả",
        "late" => $"Trễ {-DaysLeft} ngày",
        "soon" => DaysLeft == 0 ? "Hạn trả hôm nay" : $"Còn {DaysLeft} ngày",
        _ => $"Còn {DaysLeft} ngày"
    };
}
