using web_mvc_management.Common;
using web_mvc_management.Models.ViewModels.Home;
using web_mvc_management.Models.ViewModels.Reader;

namespace web_mvc_management.Services;

/// <summary>
/// Nghiệp vụ Cổng Bạn Độc Tự Phục Vụ (Client Reader Portal):
/// Sách đang mượn, Gia hạn trực tuyến, Lịch sử mượn trả, Tiền phạt cá nhân và Đặt trước tài liệu.
/// </summary>
public interface IReaderPortalService
{
    Task<ReaderLoansViewModel?> GetHomeLoansAsync(Guid readerId, CancellationToken ct = default);
    Task<ReaderPortalPageViewModel?> GetProfilePageAsync(Guid readerId, CancellationToken ct = default);
    Task<ReaderPortalPageViewModel?> GetBorrowHistoryPageAsync(Guid readerId, ReaderBorrowHistoryFilter filter, CancellationToken ct = default);
    Task<ReaderPortalPageViewModel?> GetFinesPageAsync(Guid readerId, ReaderFineFilter filter, CancellationToken ct = default);
    Task<ReaderPortalPageViewModel?> GetReservationsPageAsync(Guid readerId, CancellationToken ct = default);

    Task<ServiceResult> RenewLoanAsync(Guid readerId, Guid detailId, CancellationToken ct = default);
    Task<ServiceResult> CreateReservationAsync(Guid readerId, Guid bookId, CancellationToken ct = default);
    Task<ServiceResult> CancelReservationAsync(Guid readerId, Guid reservationId, CancellationToken ct = default);
}
