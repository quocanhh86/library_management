using web_mvc_management.Common;
using web_mvc_management.Models.ViewModels.Circulation;

namespace web_mvc_management.Services;

/// <summary>
/// Nghiệp vụ mượn – trả tại quầy: lập phiếu mượn, nhận trả, gia hạn, đặt trước, thu phạt và lịch sử phiếu mượn.
/// </summary>
public interface ICirculationService
{
    Task<CirculationDeskViewModel> GetDeskAsync(bool returning, string? card, CancellationToken ct = default);
    Task<ServiceResult<Guid>> CreateBorrowSlipAsync(BorrowSubmitViewModel model, CancellationToken ct = default);
    Task<ServiceResult> ReturnAsync(ReturnSubmitViewModel model, CancellationToken ct = default);
    Task<ServiceResult> RenewAsync(Guid detailId, CancellationToken ct = default);

    Task<BorrowHistoryPageViewModel> GetBorrowHistoryPageAsync(BorrowSlipFilter filter, CancellationToken ct = default);
    Task<BorrowSlipDetailViewModel?> GetBorrowSlipDetailAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> CancelBorrowSlipAsync(Guid id, string? reason, CancellationToken ct = default);

    Task<ReservationsPageViewModel> GetReservationsPageAsync(ReservationFilter filter, CancellationToken ct = default);
    Task<ServiceResult> CreateReservationAsync(ReservationCreateViewModel model, CancellationToken ct = default);
    Task<ServiceResult> CancelReservationAsync(Guid id, CancellationToken ct = default);

    Task<FinesPageViewModel> GetFinesPageAsync(FineFilter filter, CancellationToken ct = default);
    Task<ServiceResult> PayFineAsync(Guid id, string? paymentMethod, CancellationToken ct = default);
    Task<ServiceResult> WaiveFineAsync(Guid id, string? note, CancellationToken ct = default);
}
