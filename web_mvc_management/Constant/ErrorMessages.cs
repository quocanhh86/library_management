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
        [ErrorCode.AuthInvalidCredentials] = "Tên đăng nhập hoặc mật khẩu không chính xác.",
        [ErrorCode.AuthAccountLocked] = "Tài khoản đang bị tạm khóa để bảo vệ an toàn do nhập sai nhiều lần. Vui lòng thử lại sau {0} phút.",
        [ErrorCode.AuthAccountInactive] = "Tài khoản của bạn đã bị vô hiệu hóa hoặc chưa được kích hoạt.",
        [ErrorCode.AuthCardLocked] = "Thẻ thư viện của bạn đang bị khóa: {0}.",
        [ErrorCode.AuthStaffNotFound] = "Không tìm thấy thông tin cán bộ thư viện hợp lệ.",
        [ErrorCode.AuthAttemptsRemaining] = "Tên đăng nhập hoặc mật khẩu không chính xác. Bạn còn {0} lần thử.",
        [ErrorCode.AuthMaxAttemptsExceeded] = "Bạn đã nhập sai mật khẩu 5 lần liên tiếp. Tài khoản đã bị tạm khóa trong 15 phút.",
        [ErrorCode.AuthSessionExpired] = "Phiên đăng nhập hoặc xác thực đã hết hạn. Vui lòng thao tác lại từ đầu.",
        [ErrorCode.AuthUnauthorized] = "Vui lòng đăng nhập để thực hiện chức năng này.",
        [ErrorCode.AuthAccessDenied] = "Tài khoản của bạn không có đặc quyền truy cập vào khu vực này (403 Forbidden).",

        // 2. Đăng ký bạn đọc
        [ErrorCode.RegisterDuplicateUsername] = "Tên đăng nhập này đã được sử dụng.",
        [ErrorCode.RegisterDuplicateEmail] = "Địa chỉ email này đã được đăng ký trong hệ thống.",
        [ErrorCode.RegisterDuplicatePhone] = "Số điện thoại này đã được đăng ký trong hệ thống.",
        [ErrorCode.RegisterInvalidReaderType] = "Loại độc giả được chọn không hợp lệ hoặc đã ngừng áp dụng.",
        [ErrorCode.RegisterSessionExpired] = "Phiên đăng ký đã hết hiệu lực. Vui lòng điền lại thông tin mở thẻ.",
        [ErrorCode.RegisterFailed] = "Đăng ký mở thẻ thất bại. Vui lòng kiểm tra lại thông tin.",

        // 3. Mã xác thực OTP
        [ErrorCode.OtpInvalid] = "Mã OTP không chính xác. Bạn còn {0} lần thử.",
        [ErrorCode.OtpExpired] = "Mã OTP đã hết hiệu lực. Vui lòng yêu cầu mã mới.",
        [ErrorCode.OtpBurned] = "Mã OTP đã bị tiêu hủy do nhập sai liên tiếp {0} lần. Vui lòng yêu cầu mã mới.",
        [ErrorCode.OtpNotFound] = "Mã OTP không tồn tại hoặc đã được sử dụng.",
        [ErrorCode.OtpPurposeMismatch] = "Mục đích xác thực mã OTP không hợp lệ.",
        [ErrorCode.OtpCooldownActive] = "Vui lòng chờ {0} giây trước khi yêu cầu gửi lại mã OTP.",
        [ErrorCode.OtpSendFailed] = "Không thể gửi email chứa mã OTP. Vui lòng kiểm tra lại hòm thư hoặc thử lại sau.",
        [ErrorCode.OtpInfoIncomplete] = "Thông tin xác thực mã OTP không đầy đủ.",

        // 4. Giới hạn tần suất & Bảo mật
        [ErrorCode.RateLimitLoginExceeded] = "Bạn đã thử đăng nhập quá nhiều lần. Vui lòng thử lại sau 1 phút.",
        [ErrorCode.RateLimitOtpExceeded] = "Quá nhiều lượt thử xác thực từ IP của bạn. Vui lòng thử lại sau 1 phút.",
        [ErrorCode.RateLimitRequestExceeded] = "Lưu lượng truy cập vượt quá giới hạn cho phép. Vui lòng thử lại sau giây lát.",

        // 5. Nghiệp vụ mượn trả & Sách
        [ErrorCode.BookNotFound] = "Không tìm thấy thông tin đầu sách trong hệ thống.",
        [ErrorCode.BookCopyNotAvailable] = "Bản sao cuốn sách này hiện không ở trạng thái sẵn sàng cho mượn.",
        [ErrorCode.BookCopyDamagedOrLost] = "Cuốn sách đã được ghi nhận tình trạng hỏng hoặc mất.",
        [ErrorCode.ReaderCardExpired] = "Thẻ thư viện của bạn đã hết hạn sử dụng. Vui lòng liên hệ thủ thư để gia hạn.",
        [ErrorCode.ReaderCardLocked] = "Thẻ thư viện đang bị khóa, không thể thực hiện mượn sách.",
        [ErrorCode.ReaderMaxBorrowLimitExceeded] = "Độc giả đã đạt số lượng sách mượn tối đa cho phép ({0} cuốn).",
        [ErrorCode.ReaderHasOverdueBooks] = "Độc giả hiện có sách mượn quá hạn chưa hoàn trả.",
        [ErrorCode.ReaderHasUnpaidFines] = "Độc giả còn khoản tiền phạt chưa thanh toán ({0:N0} VNĐ).",
        [ErrorCode.BorrowSlipNotFound] = "Không tìm thấy phiếu mượn tương ứng.",
        [ErrorCode.BorrowDetailNotFound] = "Không tìm thấy chi tiết mượn sách tương ứng.",
        [ErrorCode.BookAlreadyReturned] = "Cuốn sách này đã được ghi nhận hoàn trả trước đó.",
        [ErrorCode.ReservationNotFound] = "Không tìm thấy phiếu đặt trước sách.",
        [ErrorCode.ReservationNotAvailable] = "Hiện không thể đặt trước đầu sách này.",

        // 6. Lỗi chung & Dữ liệu
        [ErrorCode.CommonValidationError] = "Dữ liệu gửi lên không hợp lệ. Vui lòng kiểm tra lại các trường thông tin.",
        [ErrorCode.CommonNotFound] = "Bản ghi yêu cầu không tồn tại trong hệ thống.",
        [ErrorCode.CommonSystemError] = "Đã có lỗi hệ thống xảy ra. Vui lòng liên hệ quản trị viên.",
        [ErrorCode.CommonInvalidRequest] = "Yêu cầu không hợp lệ hoặc thiếu thông tin bắt buộc.",
        [ErrorCode.CommonRecordInUse] = "Không thể xóa bản ghi vì đang có dữ liệu liên kết trong hệ thống."
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

