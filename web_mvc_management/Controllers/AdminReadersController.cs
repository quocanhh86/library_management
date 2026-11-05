using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

/// <summary>
/// Quản lý tài khoản & thẻ độc giả (/Admin/Readers).
/// View tương ứng: Views/AdminReaders/Index.cshtml
/// </summary>
[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Librarian)]
[Route("Admin/Readers")]
public class AdminReadersController(IReaderAdminService readerService) : BaseController
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] ReaderFilter filter, CancellationToken ct)
    {
        var page = await readerService.GetPageAsync(filter, ct);
        return View("Index", page);
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

        var result = await readerService.UpdateAsync(model, ct);
        return Done(result);
    }

    [HttpPost("Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await readerService.SoftDeleteAsync(id, ct);
        return Done(result);
    }

    [HttpPost("Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        var result = await readerService.RestoreAsync(id, ct);
        return Done(result, new { status = EntityStatus.Deleted });
    }

    // Xóa vĩnh viễn: chỉ Admin (Thủ thư không được xóa dữ liệu vĩnh viễn)
    [HttpPost("HardDelete/{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDelete(Guid id, [FromForm] string? confirmCode, CancellationToken ct)
    {
        var result = await readerService.HardDeleteAsync(id, confirmCode, ct);
        return Done(result, new { status = EntityStatus.Deleted });
    }

    private IActionResult Done(ServiceResult result, object? routeValues = null)
    {
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Index), routeValues);
    }
}
