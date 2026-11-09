using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Models.ViewModels.Reports;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Librarian)]
public class ReportsController(IReportService reportService) : BaseController
{
    [HttpGet("/Reports")]
    public async Task<IActionResult> Index([FromQuery] ReportFilter filter, CancellationToken ct)
    {
        var model = await reportService.GetReportDashboardAsync(filter, ct);
        return View("~/Views/Reports/Index.cshtml", model);
    }

    [HttpGet("/Reports/Print")]
    public async Task<IActionResult> Print([FromQuery] ReportFilter filter, CancellationToken ct)
    {
        var model = await reportService.GetReportDashboardAsync(filter, ct);
        return View("~/Views/Reports/Print.cshtml", model);
    }

    [HttpGet("/Reports/ExportOverdueCsv")]
    public async Task<IActionResult> ExportOverdueCsv(CancellationToken ct)
    {
        var bytes = await reportService.ExportOverdueLoansCsvAsync(ct);
        var filename = $"bao_cao_sach_qua_han_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv; charset=utf-8", filename);
    }
}
