using web_mvc_management.Models.ViewModels.Reports;

namespace web_mvc_management.Services;

/// <summary>
/// Nghiệp vụ báo cáo - thống kê lưu thông thư viện: mượn trả, quá hạn, top sách và tài chính thu phạt.
/// </summary>
public interface IReportService
{
    Task<ReportDashboardViewModel> GetReportDashboardAsync(ReportFilter filter, CancellationToken ct = default);
    Task<byte[]> ExportOverdueLoansCsvAsync(CancellationToken ct = default);
}
