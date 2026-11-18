using web_mvc_management.Models.ViewModels.SystemAdmin;

namespace web_mvc_management.Services;

/// <summary>
/// Dịch vụ thống kê tổng quan theo năm (/Admin/Dashboard).
/// </summary>
public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardAsync(int year, CancellationToken ct = default);
}
