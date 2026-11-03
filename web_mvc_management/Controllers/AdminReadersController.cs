using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Constant;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Librarian)]
[Route("Admin/Readers")]
public class AdminReadersController(
    ISystemAdminService systemAdminService,
    IDateTimeProvider dateTimeProvider) : BaseController
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] ReaderFilter filter, CancellationToken ct)
    {
        var paged = await systemAdminService.GetReadersPagedAsync(filter, ct);
        var readerTypes = await systemAdminService.GetReaderTypesSelectAsync(ct);

        return View(new AdminReadersPageViewModel
        {
            PagedReaders = paged,
            Filter = filter,
            ReaderTypes = readerTypes,
            Today = dateTimeProvider.Today
        });
    }

    [HttpPost("Update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(ReaderManageViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.CommonValidationError));
            return RedirectToAction(nameof(Index));
        }

        var result = await systemAdminService.UpdateReaderAccountAsync(model, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await systemAdminService.SoftDeleteReaderAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        var result = await systemAdminService.RestoreReaderAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Index), new { status = EntityStatus.Deleted });
    }

    [HttpPost("HardDelete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDelete(Guid id, [FromForm] string? confirmCode, CancellationToken ct)
    {
        var result = await systemAdminService.HardDeleteReaderAsync(id, confirmCode, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Index), new { status = EntityStatus.Deleted });
    }
}

