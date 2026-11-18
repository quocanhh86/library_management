using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

/// <summary>
/// Quản lý tài khoản &amp; thẻ độc giả (/Admin/Readers).
/// View tương ứng: Views/AdminReaders/Index.cshtml
/// </summary>
[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Librarian)]
[Route("Admin/Readers")]
public class AdminReadersController(IReaderAdminService readerService) : BaseController
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] ReaderFilter filter, CancellationToken ct) =>
        View("Index", await readerService.GetPageAsync(filter, ct));

    [HttpPost("Update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(ReaderManageViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return RedirectWithFlash(ServiceResult.ValidationError(ErrorCode.CommonValidationError), nameof(Index));
        }

        return RedirectWithFlash(await readerService.UpdateAsync(model, ct), nameof(Index));
    }

    [HttpPost("Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await readerService.SoftDeleteAsync(id, ct), nameof(Index));

    [HttpPost("Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await readerService.RestoreAsync(id, ct), nameof(Index), routeValues: new { status = EntityStatus.Deleted });

    [HttpPost("HardDelete/{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDelete(Guid id, [FromForm] string? confirmCode, CancellationToken ct) =>
        RedirectWithFlash(await readerService.HardDeleteAsync(id, confirmCode, ct), nameof(Index), routeValues: new { status = EntityStatus.Deleted });
}
