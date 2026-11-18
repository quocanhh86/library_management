using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;

namespace web_mvc_management.Services;

/// <summary>
/// Kết quả kiểm tra điều kiện mượn sách tổng hợp của độc giả.
/// </summary>
public sealed record ReaderEligibilityResult(
    IReadOnlyList<string> BlockReasons,
    int ActiveLoanCount,
    bool HasOverdueBooks,
    decimal UnpaidFines,
    decimal MaxUnpaidFine,
    ErrorCode? FirstErrorCode = null)
{
    public bool CanBorrow => BlockReasons.Count == 0;
}

/// <summary>
/// Thông tin đầu sách và độc giả đã qua kiểm tra hợp lệ để tạo phiếu đặt trước.
/// </summary>
public sealed record ReservationEligibilityContext(string ReaderFullName, string BookTitle);

/// <summary>
/// Quy tắc nghiệp vụ lưu hành dùng chung giữa Quầy thủ thư và Cổng bạn đọc (Single Source of Truth).
/// </summary>
public interface ICirculationPolicy
{
    Task<LibrarySetting?> FindSettingAsync(CancellationToken ct = default);

    Task<LibrarySetting> GetEffectiveSettingAsync(CancellationToken ct = default);

    ErrorCode? GetCardBlockingError(Reader reader, DateOnly today);

    Task<decimal> GetUnpaidFinesAsync(Guid readerId, CancellationToken ct = default);

    Task<ReaderEligibilityResult> EvaluateReaderAsync(Reader reader, CancellationToken ct = default);

    Task<ServiceResult> ValidateRenewAsync(BorrowDetail detail, CancellationToken ct = default);

    Task<ServiceResult<ReservationEligibilityContext>> ValidateReservationAsync(
        Guid readerId,
        Guid bookId,
        CancellationToken ct = default);
}
