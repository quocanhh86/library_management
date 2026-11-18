using web_mvc_management.Common;
using web_mvc_management.Models.ViewModels.Circulation;

namespace web_mvc_management.Services;

/// <summary>
/// Nghiệp vụ quản lý phiếu đặt trước tại quầy thủ thư.
/// </summary>
public interface IReservationService
{
    Task<ReservationsPageViewModel> GetReservationsPageAsync(ReservationFilter filter, CancellationToken ct = default);

    Task<ServiceResult> CreateReservationAsync(ReservationCreateViewModel model, CancellationToken ct = default);

    Task<ServiceResult> CancelReservationAsync(Guid id, CancellationToken ct = default);
}
