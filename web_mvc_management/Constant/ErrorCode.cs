namespace web_mvc_management.Constant;

/// <summary>
/// Mã định danh lỗi chuẩn dùng chung cho toàn bộ hệ thống.
/// Sử dụng để tra cứu thông điệp tương ứng trong ErrorMessages mà không cần hardcode chuỗi.
/// </summary>
public enum ErrorCode
{
    // =========================================================================
    // 1. XÁC THỰC & ĐĂNG NHẬP (AUTH & LOGIN)
    // =========================================================================
    AuthInvalidCredentials,
    AuthAccountLocked,
    AuthAccountInactive,
    AuthCardLocked,
    AuthStaffNotFound,
    AuthAttemptsRemaining,
    AuthMaxAttemptsExceeded,
    AuthSessionExpired,
    AuthUnauthorized,
    AuthAccessDenied,

    // =========================================================================
    // 2. ĐĂNG KÝ BẠN ĐỌC (REGISTRATION)
    // =========================================================================
    RegisterDuplicateUsername,
    RegisterDuplicateEmail,
    RegisterDuplicatePhone,
    RegisterInvalidReaderType,
    RegisterSessionExpired,
    RegisterFailed,

    // =========================================================================
    // 3. MÃ XÁC THỰC OTP (OTP ENGINE)
    // =========================================================================
    OtpInvalid,
    OtpExpired,
    OtpBurned,
    OtpNotFound,
    OtpPurposeMismatch,
    OtpCooldownActive,
    OtpSendFailed,
    OtpInfoIncomplete,

    // =========================================================================
    // 4. GIỚI HẠN TẦN SUẤT & BẢO MẬT (RATE LIMITING & SECURITY)
    // =========================================================================
    RateLimitLoginExceeded,
    RateLimitOtpExceeded,
    RateLimitRequestExceeded,

    // =========================================================================
    // 5. NGHIỆP VỤ MƯỢN TRẢ & SÁCH (CIRCULATION & BOOKS)
    // =========================================================================
    BookNotFound,
    BookCopyNotAvailable,
    BookCopyDamagedOrLost,
    ReaderCardExpired,
    ReaderCardLocked,
    ReaderMaxBorrowLimitExceeded,
    ReaderHasOverdueBooks,
    ReaderHasUnpaidFines,
    BorrowSlipNotFound,
    BorrowDetailNotFound,
    BookAlreadyReturned,
    ReservationNotFound,
    ReservationNotAvailable,

    // =========================================================================
    // 6. LỖI CHUNG & DỮ LIỆU (COMMON & SYSTEM)
    // =========================================================================
    CommonValidationError,
    CommonNotFound,
    CommonSystemError,
    CommonInvalidRequest,
    CommonRecordInUse
}

