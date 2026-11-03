using web_mvc_management.Models.ViewModels.Admin;

namespace web_mvc_management.Services;

public interface IAdminPortalService
{
    Task<AdminDashboardViewModel> GetDashboardAsync(CancellationToken ct = default);
    Task<AdminListViewModel?> GetListAsync(string module, string? keyword, string? status, string? group, int pageNo, int pageItemSize, CancellationToken ct = default);
    Task<AdminCirculationViewModel> GetCirculationAsync(bool returning, string? card, CancellationToken ct = default);
    Task<AdminSettingsViewModel> GetSettingsAsync(CancellationToken ct = default);
}
