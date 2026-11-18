using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Constant;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Reports;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Librarian)]
public class ReportsController(
    IReportService reportService,
    IDateTimeProvider dateTimeProvider) : BaseController
{
    [HttpGet("/Reports")]
    public async Task<IActionResult> Index([FromQuery] ReportFilter filter, CancellationToken ct) =>
        View("~/Views/Reports/Index.cshtml", await reportService.GetReportDashboardAsync(filter, ct));

    [HttpGet("/Reports/Print")]
    public async Task<IActionResult> Print([FromQuery] ReportFilter filter, CancellationToken ct) =>
        View("~/Views/Reports/Print.cshtml", await reportService.GetReportDashboardAsync(filter, ct));

    [HttpGet("/Reports/ExportOverdueCsv")]
    public async Task<IActionResult> ExportOverdueCsv(CancellationToken ct)
    {
        var bytes = await reportService.ExportOverdueLoansCsvAsync(ct);
        var filename = $"bao_cao_sach_qua_han_{dateTimeProvider.Now:yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv; charset=utf-8", filename);
    }
}
