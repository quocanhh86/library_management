using web_mvc_management.Common;
using web_mvc_management.Models.ViewModels.Circulation;

namespace web_mvc_management.Services;

/// <summary>
/// Nghiệp vụ quầy lưu hành: mượn, trả, gia hạn và quản lý lịch sử phiếu mượn.
/// </summary>
public interface ILoanDeskService
{
    Task<CirculationDeskViewModel> GetDeskAsync(bool returning, string? card, CancellationToken ct = default);

    Task<ServiceResult<Guid>> CreateBorrowSlipAsync(BorrowSubmitViewModel model, CancellationToken ct = default);

    Task<ServiceResult> ReturnAsync(ReturnSubmitViewModel model, CancellationToken ct = default);

    Task<ServiceResult> RenewAsync(Guid detailId, CancellationToken ct = default);

    Task<BorrowHistoryPageViewModel> GetBorrowHistoryPageAsync(BorrowSlipFilter filter, CancellationToken ct = default);

    Task<BorrowSlipDetailViewModel?> GetBorrowSlipDetailAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> CancelBorrowSlipAsync(Guid id, string? reason, CancellationToken ct = default);
}
