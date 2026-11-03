using System;
using System.Collections.Generic;

namespace web_mvc_management.Constant;

/// <summary>
/// Quản lý tập trung toàn bộ thông báo lỗi trong hệ thống theo mã ErrorCode.
/// Tuyệt đối không hardcode chuỗi thông báo lỗi trực tiếp trong Controller/Service.
/// </summary>
public static class ErrorMessages
{
    private static readonly Dictionary<ErrorCode, string> Messages = new()
    {
        // 1. Xác thực & Đăng nhập
        [ErrorCode.AuthInvalidCredentials] = "Tên đăng nhập hoặc mật khẩu không đúng.",
        [ErrorCode.AuthAccountLocked] = "Tài khoản đang tạm khóa do nhập sai nhiều lần. Thử lại lúc {1} (sau {0} phút).",
        [ErrorCode.AuthAccountInactive] = "Tài khoản chưa được kích hoạt hoặc đã bị vô hiệu hóa. Liên hệ thủ thư để được hỗ trợ.",
        [ErrorCode.AuthCardLocked] = "Thẻ thư viện của bạn đang bị khóa: {0}.",
        [ErrorCode.AuthStaffNotFound] = "Email này chưa được cấp quyền cán bộ. Kiểm tra lại email hoặc liên hệ quản trị viên.",
        [ErrorCode.AuthAttemptsRemaining] = "Tên đăng nhập hoặc mật khẩu không đúng. Bạn còn {0} lần thử.",
        [ErrorCode.AuthMaxAttemptsExceeded] = "Tài khoản tạm khóa 15 phút do nhập sai mật khẩu 5 lần. Thử lại lúc {0}.",
        [ErrorCode.AuthSessionExpired] = "Phiên xác thực đã hết hạn. Nhập lại thông tin đăng nhập để nhận mã mới.",
        [ErrorCode.AuthUnauthorized] = "Đăng nhập để dùng chức năng này.",
        [ErrorCode.AuthAccessDenied] = "Tài khoản của bạn không được mở trang này.",

        // 2. Đăng ký bạn đọc
        [ErrorCode.RegisterDuplicateUsername] = "Tên đăng nhập này đã được sử dụng.",
        [ErrorCode.RegisterDuplicateEmail] = "Email này đã được dùng để đăng ký thẻ. Đăng nhập bằng email này, hoặc dùng email khác.",
        [ErrorCode.RegisterDuplicatePhone] = "Số điện thoại này đã được dùng để đăng ký thẻ.",
        [ErrorCode.RegisterInvalidReaderType] = "Loại độc giả này không còn áp dụng. Chọn loại khác.",
        [ErrorCode.RegisterSessionExpired] = "Phiên đăng ký đã hết hạn. Điền lại thông tin để nhận mã mới.",
        [ErrorCode.RegisterFailed] = "Chưa đăng ký được thẻ. Kiểm tra lại các ô được đánh dấu.",

        // 3. Mã xác thực OTP
        [ErrorCode.OtpInvalid] = "Mã không đúng. Bạn còn {0} lần thử.",
        [ErrorCode.OtpExpired] = "Mã đã hết hạn. Bấm Gửi lại mã để nhận mã mới.",
        [ErrorCode.OtpBurned] = "Mã đã mất hiệu lực sau {0} lần nhập sai. Bấm Gửi lại mã để nhận mã mới.",
        [ErrorCode.OtpNotFound] = "Mã này đã được dùng hoặc không còn hiệu lực. Bấm Gửi lại mã để nhận mã mới.",
        [ErrorCode.OtpPurposeMismatch] = "Không xác định được bước xác thực. Đăng nhập lại từ đầu.",
        [ErrorCode.OtpCooldownActive] = "Chờ {0} giây nữa rồi bấm Gửi lại mã.",
        [ErrorCode.OtpSendFailed] = "Chưa gửi được email chứa mã. Kiểm tra địa chỉ email rồi thử lại sau ít phút.",
        [ErrorCode.OtpInfoIncomplete] = "Thiếu thông tin để kiểm tra mã. Đăng nhập lại từ đầu.",

        // 4. Giới hạn tần suất & Bảo mật
        [ErrorCode.RateLimitLoginExceeded] = "Bạn đã thử đăng nhập quá nhiều lần. Thử lại sau 1 phút.",
        [ErrorCode.RateLimitOtpExceeded] = "Máy của bạn đã thử nhập mã quá nhiều lần. Thử lại sau 1 phút.",
        [ErrorCode.RateLimitRequestExceeded] = "Có quá nhiều yêu cầu cùng lúc. Thử lại sau vài giây.",

        // 5. Nghiệp vụ mượn trả & Sách
        [ErrorCode.BookNotFound] = "Không tìm thấy đầu sách này.",
        [ErrorCode.BookIsbnAlreadyExists] = "Mã ISBN này đã tồn tại trên một đầu sách khác trong thư viện.",
        [ErrorCode.BookHasBorrowedCopies] = "Chưa xóa được đầu sách vì đang có bản sao được mượn hoặc giữ chỗ.",
        [ErrorCode.BookCopyNotFound] = "Không tìm thấy bản sao sách tương ứng.",
        [ErrorCode.BookCopyNotAvailable] = "Bản sao này không sẵn sàng cho mượn. Quét bản khác.",
        [ErrorCode.BookCopyDamagedOrLost] = "Bản sao này đã ghi nhận hỏng hoặc mất.",
        [ErrorCode.BookCopyBarcodeAlreadyExists] = "Mã vạch bản sao này đã tồn tại trong hệ thống.",
        [ErrorCode.BookCopyCurrentlyBorrowed] = "Bản sao đang được độc giả mượn nên không thể thanh lý hoặc đổi trạng thái trực tiếp.",
        [ErrorCode.BookCopyAlreadyDisposed] = "Bản sao này đã được thanh lý trước đó.",
        [ErrorCode.CategoryNotFound] = "Không tìm thấy thể loại sách yêu cầu.",
        [ErrorCode.CategoryNameAlreadyExists] = "Tên thể loại sách này đã tồn tại.",
        [ErrorCode.AuthorNotFound] = "Không tìm thấy thông tin tác giả yêu cầu.",
        [ErrorCode.AuthorNameAlreadyExists] = "Tác giả này đã tồn tại trong danh mục.",
        [ErrorCode.PublisherNotFound] = "Không tìm thấy thông tin nhà xuất bản yêu cầu.",
        [ErrorCode.PublisherNameAlreadyExists] = "Tên nhà xuất bản này đã tồn tại.",
        [ErrorCode.ShelfNotFound] = "Không tìm thấy vị trí kệ sách yêu cầu.",
        [ErrorCode.ShelfCodeAlreadyExists] = "Mã vị trí kệ sách này đã tồn tại.",
        [ErrorCode.ReaderNotFound] = "Không tìm thấy hồ sơ độc giả yêu cầu.",
        [ErrorCode.ReaderCardExpired] = "Thẻ thư viện đã hết hạn. Gia hạn thẻ tại quầy thủ thư.",
        [ErrorCode.ReaderCardLocked] = "Thẻ thư viện đang bị khóa nên chưa mượn được sách.",
        [ErrorCode.ReaderMaxBorrowLimitExceeded] = "Độc giả đang mượn đủ {0} cuốn theo hạn mức. Trả bớt sách trước khi mượn thêm.",
        [ErrorCode.ReaderHasOverdueBooks] = "Độc giả còn sách quá hạn chưa trả. Nhận trả sách trước khi cho mượn.",
        [ErrorCode.ReaderHasUnpaidFines] = "Độc giả còn nợ phạt {0}. Thu phạt trước khi cho mượn.",
        [ErrorCode.ReaderTypeNotFound] = "Không tìm thấy loại độc giả yêu cầu.",
        [ErrorCode.ReaderTypeCodeAlreadyExists] = "Mã loại độc giả này đã tồn tại.",
        [ErrorCode.BorrowSlipNotFound] = "Không tìm thấy phiếu mượn tương ứng.",
        [ErrorCode.BorrowDetailNotFound] = "Không tìm thấy chi tiết mượn sách tương ứng.",
        [ErrorCode.BookAlreadyReturned] = "Bản sao này đã được trả trước đó.",
        [ErrorCode.ReservationNotFound] = "Không tìm thấy phiếu đặt trước sách.",
        [ErrorCode.ReservationNotAvailable] = "Hiện không thể đặt trước đầu sách này.",
        [ErrorCode.BorrowListEmpty] = "Phiếu chưa có bản sao nào. Quét mã vạch trước khi lập phiếu.",
        [ErrorCode.BorrowDuplicateBarcode] = "Mã vạch {0} được quét hai lần. Bỏ bớt một dòng.",
        [ErrorCode.BookCopyReservedForOther] = "Bản sao {0} đang được giữ cho độc giả khác đặt trước.",
        [ErrorCode.RenewalLimitReached] = "Đã gia hạn đủ {0} lần cho phép.",
        [ErrorCode.RenewOverdueNotAllowed] = "Sách đã quá hạn nên không gia hạn được. Nhận trả sách và thu phạt.",
        [ErrorCode.RenewBlockedByReservation] = "Đầu sách đang có độc giả khác đặt trước nên không gia hạn được.",
        [ErrorCode.ReturnConditionInvalid] = "Tình trạng sách khi trả chưa hợp lệ.",
        [ErrorCode.FineNotFound] = "Không tìm thấy khoản phạt tương ứng.",
        [ErrorCode.FineAlreadySettled] = "Khoản phạt này đã được xử lý trước đó.",
        [ErrorCode.FineWaiveNoteRequired] = "Nhập lý do trước khi miễn phạt.",
        [ErrorCode.ReservationDuplicate] = "Độc giả đã đặt trước đầu sách này và đang chờ.",
        [ErrorCode.ReservationBookAvailable] = "Đầu sách còn bản sẵn sàng trong kho. Cho mượn trực tiếp thay vì đặt trước.",
        [ErrorCode.ReaderAlreadyBorrowingBook] = "Độc giả đang mượn một bản của đầu sách này.",
        [ErrorCode.ReservationAlreadyClosed] = "Phiếu đặt trước này đã kết thúc.",

        // 6. Quản trị hệ thống
        [ErrorCode.StaffEmailAlreadyExists] = "Địa chỉ email này đã được gán cho một cán bộ khác.",
        [ErrorCode.StaffUsernameAlreadyExists] = "Tên đăng nhập cán bộ này đã tồn tại.",
        [ErrorCode.StaffCannotModifySelf] = "Không thể tự khóa hoặc tự hạ quyền tài khoản của chính mình.",
        [ErrorCode.LibrarySettingNotFound] = "Chưa tìm thấy bản ghi cấu hình quy định thư viện.",

        // 7. Lỗi chung & Dữ liệu
        [ErrorCode.CommonValidationError] = "Thông tin chưa hợp lệ. Kiểm tra lại các ô được đánh dấu.",
        [ErrorCode.CommonNotFound] = "Không tìm thấy dữ liệu này. Có thể nó đã bị xóa.",
        [ErrorCode.CommonSystemError] = "Hệ thống gặp lỗi khi xử lý yêu cầu. Thử lại; nếu vẫn lỗi, báo quản trị viên.",
        [ErrorCode.CommonInvalidRequest] = "Thiếu thông tin bắt buộc. Kiểm tra lại các ô được đánh dấu.",
        [ErrorCode.CommonRecordInUse] = "Chưa xóa được vì dữ liệu này đang được dùng ở nơi khác.",
        [ErrorCode.HardDeleteRequiresSoftDeleteFirst] = "Bắt buộc thực hiện Bước 1: Chuyển trạng thái bản ghi sang Đã xóa mềm (DELETED) trước khi xóa vĩnh viễn.",
        [ErrorCode.HardDeleteConfirmInvalid] = "Bước 3 chưa hợp lệ: Vui lòng nhập đúng mã xác nhận 'XOA' để xóa vĩnh viễn bản ghi."
    };

    /// <summary>
    /// Lấy chuỗi thông báo lỗi tĩnh theo mã ErrorCode.
    /// </summary>
    public static string Get(ErrorCode code)
    {
        return Messages.TryGetValue(code, out var message) ? message : "Đã có lỗi xảy ra.";
    }

    /// <summary>
    /// Lấy chuỗi thông báo lỗi động có tham số format theo mã ErrorCode.
    /// </summary>
    public static string Format(ErrorCode code, params object[] args)
    {
        var template = Get(code);
        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}
