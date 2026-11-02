using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Constant;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class AdminPortalControllerBase(IAdminPortalService service) : BaseController
{
    protected void SetScreen(ScreenId screen, string title)
    {
        ViewData["AdminPortal"] = true;
        ViewData["Screen"] = screen;
        ViewData["Title"] = title;
    }

    protected IActionResult Denied()
    {
        if (ViewData["AdminPreview"] is not true) return Forbid();
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("~/Views/Account/AccessDenied.cshtml");
    }

    protected async Task<IActionResult> DashboardPage(bool reports, CancellationToken ct)
    {
        SetScreen(reports ? ScreenId.ReportsStatistics : ScreenId.AdminDashboard, reports ? "Báo cáo thư viện" : "Tổng quan vận hành");
        return View(reports ? "~/Views/Admin/Reports.cshtml" : "~/Views/Admin/Dashboard.cshtml", await service.GetDashboardAsync(ct));
    }

    protected async Task<IActionResult> ListPage(string module, string? keyword, string? status, string? group, int pageNo, int pageItemSize, CancellationToken ct)
    {
        var model = await service.GetListAsync(module, keyword, status, group, pageNo, pageItemSize, ct);
        if (model is null) return NotFound();
        var roles = Screens.Get(model.Screen).AllowedRoles;
        if (roles is not null && !roles.Any(User.IsInRole)) return Denied();
        SetScreen(model.Screen, model.Title);
        if (IsAjaxRequest)
        {
            Response.Headers["X-Admin-Partial"] = "1";
            return PartialView("~/Views/Admin/_ListResults.cshtml", model);
        }
        return View("~/Views/Admin/Index.cshtml", model);
    }

    protected async Task<IActionResult> CirculationPage(bool returning, string? card, CancellationToken ct)
    {
        SetScreen(returning ? ScreenId.CirculationReturn : ScreenId.CirculationBorrow, returning ? "Trả sách" : "Lập phiếu mượn");
        return View("~/Views/Admin/Circulation.cshtml", await service.GetCirculationAsync(returning, card, ct));
    }

    protected async Task<IActionResult> SettingsPage(CancellationToken ct)
    {
        if (!User.IsInRole(AppRoles.Admin)) return Denied();
        SetScreen(ScreenId.SystemSettings, "Quy định mượn trả");
        return View("~/Views/Admin/Settings.cshtml", await service.GetSettingsAsync(ct));
    }
}

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Librarian)]
public class AdminController(IAdminPortalService service) : AdminPortalControllerBase(service)
{
    [HttpGet("/Admin")]
    [HttpGet("/Admin/Dashboard")]
    public Task<IActionResult> Dashboard(CancellationToken ct) => DashboardPage(false, ct);

    [HttpGet("/Admin/{module:regex(^(Books|BookCopies|Authors|Categories|Publishers|Shelves|Readers|ReaderTypes)$)}")]
    public Task<IActionResult> Index(string module, string? keyword, string? status, string? group, int pageNo = 1, int pageItemSize = 10, CancellationToken ct = default) => ListPage(module, keyword, status, group, pageNo, pageItemSize, ct);

    [HttpGet("/System/Staff")]
    [Authorize(Roles = AppRoles.Admin)]
    public Task<IActionResult> Staff(string? keyword, string? status, string? group, int pageNo = 1, int pageItemSize = 10, CancellationToken ct = default) => ListPage("staff", keyword, status, group, pageNo, pageItemSize, ct);

    [HttpGet("/System/Settings")]
    [Authorize(Roles = AppRoles.Admin)]
    public Task<IActionResult> Settings(CancellationToken ct) => SettingsPage(ct);

    [HttpGet("/Circulation/Borrow")]
    public Task<IActionResult> Borrow(string? card, CancellationToken ct) => CirculationPage(false, card, ct);

    [HttpGet("/Circulation/Return")]
    public Task<IActionResult> Return(string? card, CancellationToken ct) => CirculationPage(true, card, ct);

    [HttpGet("/Circulation/History")]
    public Task<IActionResult> History(string? keyword, string? status, int pageNo = 1, int pageItemSize = 10, CancellationToken ct = default) => ListPage("history", keyword, status, null, pageNo, pageItemSize, ct);

    [HttpGet("/Fines")]
    public Task<IActionResult> Fines(string? keyword, string? status, int pageNo = 1, int pageItemSize = 10, CancellationToken ct = default) => ListPage("fines", keyword, status, null, pageNo, pageItemSize, ct);

    [HttpGet("/Reports")]
    public Task<IActionResult> Reports(CancellationToken ct) => DashboardPage(true, ct);
}
