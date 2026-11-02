using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using web_mvc_management.Constant;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

/// <summary>Read-only previews. Claims apply only to this request; no authentication cookie is issued.</summary>
[AllowAnonymous]
public class DevAdminController(IAdminPortalService service, IWebHostEnvironment environment) : AdminPortalControllerBase(service)
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!environment.IsDevelopment())
        {
            context.Result = NotFound();
            return;
        }
        var role = Request.Query["role"] == AppRoles.Librarian ? AppRoles.Librarian : AppRoles.Admin;
        HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, role == AppRoles.Admin ? "Nguyễn Quốc Anh" : "Trần Thu Hà"), new Claim(ClaimTypes.Role, role)], "AdminPreview"));
        ViewData["AdminPreview"] = true;
        base.OnActionExecuting(context);
    }

    [HttpGet("/dev/Admin")]
    [HttpGet("/dev/Admin/Dashboard")]
    public Task<IActionResult> Dashboard(CancellationToken ct) => DashboardPage(false, ct);

    [HttpGet("/dev/Admin/{module:regex(^(Books|BookCopies|Authors|Categories|Publishers|Shelves|Readers|ReaderTypes)$)}")]
    public Task<IActionResult> Index(string module, string? keyword, string? status, string? group, int pageNo = 1, int pageItemSize = 10, CancellationToken ct = default) => ListPage(module, keyword, status, group, pageNo, pageItemSize, ct);

    [HttpGet("/dev/System/Staff")]
    public Task<IActionResult> Staff(string? keyword, string? status, string? group, int pageNo = 1, int pageItemSize = 10, CancellationToken ct = default) => ListPage("staff", keyword, status, group, pageNo, pageItemSize, ct);

    [HttpGet("/dev/System/Settings")]
    public Task<IActionResult> Settings(CancellationToken ct) => SettingsPage(ct);

    [HttpGet("/dev/Circulation/Borrow")]
    public Task<IActionResult> Borrow(string? card, CancellationToken ct) => CirculationPage(false, card, ct);

    [HttpGet("/dev/Circulation/Return")]
    public Task<IActionResult> Return(string? card, CancellationToken ct) => CirculationPage(true, card, ct);

    [HttpGet("/dev/Circulation/History")]
    public Task<IActionResult> History(string? keyword, string? status, int pageNo = 1, int pageItemSize = 10, CancellationToken ct = default) => ListPage("history", keyword, status, null, pageNo, pageItemSize, ct);

    [HttpGet("/dev/Fines")]
    public Task<IActionResult> Fines(string? keyword, string? status, int pageNo = 1, int pageItemSize = 10, CancellationToken ct = default) => ListPage("fines", keyword, status, null, pageNo, pageItemSize, ct);

    [HttpGet("/dev/Reports")]
    public Task<IActionResult> Reports(CancellationToken ct) => DashboardPage(true, ct);
}
