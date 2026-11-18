using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Models.ViewModels.AdminCatalog;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Librarian)]
[Route("Admin")]
public class AdminMasterDataController(ICatalogMasterService masterService) : BaseController
{
    // =========================================================================
    // 1. THỂ LOẠI SÁCH (/Admin/Categories)
    // =========================================================================
    [HttpGet("Categories")]
    public async Task<IActionResult> Categories([FromQuery] BaseFilter filter, CancellationToken ct) =>
        View("Categories", new CategoryPageViewModel
        {
            PagedItems = await masterService.GetCategoriesPagedAsync(filter, ct),
            Filter = filter
        });

    [HttpPost("Categories/Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(CategoryFormViewModel model, CancellationToken ct) =>
        await HandleSaveAsync(() => masterService.SaveCategoryAsync(model, ct), nameof(Categories));

    [HttpPost("Categories/Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await masterService.DeleteCategoryAsync(id, ct), nameof(Categories));

    [HttpPost("Categories/Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreCategory(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await masterService.RestoreCategoryAsync(id, ct), nameof(Categories), routeValues: new { status = EntityStatus.Deleted });

    [HttpPost("Categories/HardDelete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDeleteCategory(Guid id, [FromForm] string? confirmCode, CancellationToken ct) =>
        RedirectWithFlash(await masterService.HardDeleteCategoryAsync(id, confirmCode, ct), nameof(Categories), routeValues: new { status = EntityStatus.Deleted });

    // =========================================================================
    // 2. TÁC GIẢ (/Admin/Authors)
    // =========================================================================
    [HttpGet("Authors")]
    public async Task<IActionResult> Authors([FromQuery] BaseFilter filter, CancellationToken ct) =>
        View("Authors", new AuthorPageViewModel
        {
            PagedItems = await masterService.GetAuthorsPagedAsync(filter, ct),
            Filter = filter
        });

    [HttpPost("Authors/Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAuthor(AuthorFormViewModel model, CancellationToken ct) =>
        await HandleSaveAsync(() => masterService.SaveAuthorAsync(model, ct), nameof(Authors));

    [HttpPost("Authors/Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAuthor(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await masterService.DeleteAuthorAsync(id, ct), nameof(Authors));

    [HttpPost("Authors/Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreAuthor(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await masterService.RestoreAuthorAsync(id, ct), nameof(Authors), routeValues: new { status = EntityStatus.Deleted });

    [HttpPost("Authors/HardDelete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDeleteAuthor(Guid id, [FromForm] string? confirmCode, CancellationToken ct) =>
        RedirectWithFlash(await masterService.HardDeleteAuthorAsync(id, confirmCode, ct), nameof(Authors), routeValues: new { status = EntityStatus.Deleted });

    // =========================================================================
    // 3. NHÀ XUẤT BẢN (/Admin/Publishers)
    // =========================================================================
    [HttpGet("Publishers")]
    public async Task<IActionResult> Publishers([FromQuery] BaseFilter filter, CancellationToken ct) =>
        View("Publishers", new PublisherPageViewModel
        {
            PagedItems = await masterService.GetPublishersPagedAsync(filter, ct),
            Filter = filter
        });

    [HttpPost("Publishers/Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePublisher(PublisherFormViewModel model, CancellationToken ct) =>
        await HandleSaveAsync(() => masterService.SavePublisherAsync(model, ct), nameof(Publishers));

    [HttpPost("Publishers/Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePublisher(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await masterService.DeletePublisherAsync(id, ct), nameof(Publishers));

    [HttpPost("Publishers/Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestorePublisher(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await masterService.RestorePublisherAsync(id, ct), nameof(Publishers), routeValues: new { status = EntityStatus.Deleted });

    [HttpPost("Publishers/HardDelete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDeletePublisher(Guid id, [FromForm] string? confirmCode, CancellationToken ct) =>
        RedirectWithFlash(await masterService.HardDeletePublisherAsync(id, confirmCode, ct), nameof(Publishers), routeValues: new { status = EntityStatus.Deleted });

    // =========================================================================
    // 4. VỊ TRÍ KỆ SÁCH (/Admin/Shelves)
    // =========================================================================
    [HttpGet("Shelves")]
    public async Task<IActionResult> Shelves([FromQuery] BaseFilter filter, CancellationToken ct) =>
        View("Shelves", new ShelfPageViewModel
        {
            PagedItems = await masterService.GetShelvesPagedAsync(filter, ct),
            Filter = filter
        });

    [HttpPost("Shelves/Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveShelf(ShelfFormViewModel model, CancellationToken ct) =>
        await HandleSaveAsync(() => masterService.SaveShelfAsync(model, ct), nameof(Shelves));

    [HttpPost("Shelves/Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteShelf(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await masterService.DeleteShelfAsync(id, ct), nameof(Shelves));

    [HttpPost("Shelves/Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreShelf(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await masterService.RestoreShelfAsync(id, ct), nameof(Shelves), routeValues: new { status = EntityStatus.Deleted });

    [HttpPost("Shelves/HardDelete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDeleteShelf(Guid id, [FromForm] string? confirmCode, CancellationToken ct) =>
        RedirectWithFlash(await masterService.HardDeleteShelfAsync(id, confirmCode, ct), nameof(Shelves), routeValues: new { status = EntityStatus.Deleted });

    private async Task<IActionResult> HandleSaveAsync(Func<Task<ServiceResult>> saveAction, string redirectAction)
    {
        if (!ModelState.IsValid)
        {
            return RedirectWithFlash(ServiceResult.ValidationError(ErrorCode.CommonValidationError), redirectAction);
        }

        return RedirectWithFlash(await saveAction(), redirectAction);
    }
}
