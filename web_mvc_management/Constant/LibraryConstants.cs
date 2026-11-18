namespace web_mvc_management.Constant;

/// <summary>
/// Các hằng số nghiệp vụ thư viện ánh xạ với các trường trong CSDL.
/// </summary>
public static class LibraryConstants
{
    /// <summary>
    /// Trạng thái bản sao sách (book_copies.copy_status).
    /// </summary>
    public static class CopyStatus
    {
        public const string Available = "AVAILABLE"; // Sẵn sàng cho mượn
        public const string Borrowed = "BORROWED";   // Đang được mượn
        public const string Reserved = "RESERVED";   // Đang được giữ chỗ đặt trước
        public const string Damaged = "DAMAGED";     // Bị hư hỏng
        public const string Lost = "LOST";           // Bị mất
        public const string Disposed = "DISPOSED";   // Đã thanh lý
    }

    /// <summary>
    /// Phân loại hình ảnh của đầu sách (book_images.image_type).
    /// </summary>
    public static class BookImageType
    {
        public const string Cover = "COVER";                     // Ảnh bìa trước (chính)
        public const string BackCover = "BACK_COVER";            // Ảnh bìa sau
        public const string TableOfContents = "TABLE_OF_CONTENTS"; // Ảnh mục lục
        public const string PreviewPage = "PREVIEW_PAGE";        // Ảnh trang đọc thử / nội dung
        public const string Gallery = "GALLERY";                 // Ảnh minh họa bổ sung

        public static string GetLabel(string? type) => type switch
        {
            Cover => "Ảnh bìa trước",
            BackCover => "Ảnh bìa sau",
            TableOfContents => "Mục lục",
            PreviewPage => "Trang đọc thử",
            Gallery => "Ảnh minh họa",
            _ => "Trang sách"
        };
    }

    /// <summary>
    /// Trạng thái thẻ độc giả (readers.card_status).
    /// </summary>
    public static class CardStatus
    {
        public const string Active = "ACTIVE";     // Thẻ đang hoạt động
        public const string Locked = "LOCKED";     // Thẻ bị khóa
        public const string Expired = "EXPIRED";   // Thẻ đã hết hạn
    }

    /// <summary>
    /// Loại phạt (fines.fine_type).
    /// </summary>
    public static class FineType
    {
        public const string Overdue = "OVERDUE";   // Trả quá hạn
        public const string Damaged = "DAMAGED";   // Làm hỏng sách
        public const string Lost = "LOST";         // Làm mất sách
    }

    /// <summary>
    /// Trạng thái phiếu phạt (fines.fine_status).
    /// </summary>
    public static class FineStatus
    {
        public const string Unpaid = "UNPAID";     // Chưa nộp phạt
        public const string Paid = "PAID";         // Đã nộp phạt
        public const string Waived = "WAIVED";     // Được miễn giảm
        public const string Cancelled = "CANCELLED"; // Đã hủy
    }

    /// <summary>
    /// Phương thức thanh toán (fines.payment_method).
    /// </summary>
    public static class PaymentMethod
    {
        public const string Cash = "CASH";                 // Tiền mặt
        public const string BankTransfer = "TRANSFER";     // Chuyển khoản
        public const string Online = "ONLINE";             // Cổng thanh toán trực tuyến
    }

    /// <summary>
    /// Trạng thái đặt trước (reservations.reservation_status).
    /// </summary>
    public static class ReservationStatus
    {
        public const string Pending = "PENDING";     // Đang chờ có sách
        public const string Ready = "READY";         // Sách đã về, chờ độc giả đến lấy
        public const string Fulfilled = "FULFILLED"; // Đã nhận sách mượn
        public const string Cancelled = "CANCELLED"; // Đã hủy đặt
        public const string Expired = "EXPIRED";     // Hết hạn giữ chỗ
    }

    /// <summary>
    /// Tình trạng sách khi trả (borrow_details.return_condition).
    /// </summary>
    public static class ReturnCondition
    {
        public const string Good = "GOOD";           // Nguyên vẹn bình thường
        public const string Damaged = "DAMAGED";     // Bị hư hỏng
        public const string Lost = "LOST";           // Báo mất sách
    }

    /// <summary>
    /// Mục đích mã xác thực OTP (otps.purpose).
    /// </summary>
    public static class OtpPurpose
    {
        public const string Register = "REGISTER";
        public const string ReaderLogin = "READER_LOGIN";
        public const string StaffLogin = "STAFF_LOGIN";
    }

    /// <summary>
    /// Tiền tố mã thẻ thư viện theo loại độc giả.
    /// </summary>
    public static class CardPrefix
    {
        public const string Staff = "GV";
        public const string Guest = "KH";
        public const string Student = "SV";
        public const string Fallback = "DG";
    }

    /// <summary>
    /// Giá trị cấu hình mặc định khi bảng library_settings chưa có bản ghi hoạt động.
    /// </summary>
    public static class Defaults
    {
        public const decimal OverdueFinePerDay = 2000m;
        public const short DamagedFinePercent = 50;
        public const short LostFinePercent = 100;
        public const decimal MaxUnpaidFine = 50000m;
        public const short ReservationHoldHours = 48;
        public const int BorrowCancelWindowHours = 24;
    }

    public const string HardDeleteConfirmToken = "XOA";

    /// <summary>
    /// Kiểm tra chuỗi xác nhận xóa cứng ('XOA', không phân biệt hoa/thường và khoảng trắng).
    /// </summary>
    public static bool IsValidHardDeleteConfirm(string? confirmCode) =>
        string.Equals(confirmCode?.Trim(), HardDeleteConfirmToken, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Chuẩn hóa trạng thái đầu vào biểu mẫu chỉ nhận ACTIVE hoặc INACTIVE (chống giả mạo DELETED qua form).
    /// </summary>
    public static string NormalizeActiveOrInactive(string? rawStatus) =>
        string.Equals(rawStatus?.Trim(), EntityStatus.Inactive, StringComparison.OrdinalIgnoreCase)
            ? EntityStatus.Inactive
            : EntityStatus.Active;
}
