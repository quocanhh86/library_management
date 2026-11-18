using web_mvc_management.Common;
using web_mvc_management.Models.ViewModels.Circulation;

namespace web_mvc_management.Services;

/// <summary>
/// Nghiệp vụ thu phạt và miễn giảm tiền phạt tại quầy.
/// </summary>
public interface IFineService
{
    Task<FinesPageViewModel> GetFinesPageAsync(FineFilter filter, CancellationToken ct = default);

    Task<ServiceResult> PayFineAsync(Guid id, string? paymentMethod, CancellationToken ct = default);

    Task<ServiceResult> WaiveFineAsync(Guid id, string? note, CancellationToken ct = default);
}
