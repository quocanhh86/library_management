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
    public async Task<IActionResult> Categories([FromQuery] BaseFilter filter, CancellationToken ct)
    {
        var paged = await masterService.GetCategoriesPagedAsync(filter, ct);
        return View("Categories", new CategoryPageViewModel
        {
            PagedItems = paged,
            Filter = filter
        });
    }

    [HttpPost("Categories/Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(CategoryFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.CommonValidationError));
            return RedirectToAction(nameof(Categories));
        }

        var result = await masterService.SaveCategoryAsync(model, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Categories));
    }

    [HttpPost("Categories/Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
    {
        var result = await masterService.DeleteCategoryAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Categories));
    }

    [HttpPost("Categories/Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreCategory(Guid id, CancellationToken ct)
    {
        var result = await masterService.RestoreCategoryAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Categories), new { status = EntityStatus.Deleted });
    }

    [HttpPost("Categories/HardDelete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDeleteCategory(Guid id, [FromForm] string? confirmCode, CancellationToken ct)
    {
        var result = await masterService.HardDeleteCategoryAsync(id, confirmCode, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Categories), new { status = EntityStatus.Deleted });
    }

    // =========================================================================
    // 2. TÁC GIẢ (/Admin/Authors)
    // =========================================================================
    [HttpGet("Authors")]
    public async Task<IActionResult> Authors([FromQuery] BaseFilter filter, CancellationToken ct)
    {
        var paged = await masterService.GetAuthorsPagedAsync(filter, ct);
        return View("Authors", new AuthorPageViewModel
        {
            PagedItems = paged,
            Filter = filter
        });
    }

    [HttpPost("Authors/Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAuthor(AuthorFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.CommonValidationError));
            return RedirectToAction(nameof(Authors));
        }

        var result = await masterService.SaveAuthorAsync(model, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Authors));
    }

    [HttpPost("Authors/Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAuthor(Guid id, CancellationToken ct)
    {
        var result = await masterService.DeleteAuthorAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Authors));
    }

    [HttpPost("Authors/Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreAuthor(Guid id, CancellationToken ct)
    {
        var result = await masterService.RestoreAuthorAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Authors), new { status = EntityStatus.Deleted });
    }

    [HttpPost("Authors/HardDelete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDeleteAuthor(Guid id, [FromForm] string? confirmCode, CancellationToken ct)
    {
        var result = await masterService.HardDeleteAuthorAsync(id, confirmCode, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Authors), new { status = EntityStatus.Deleted });
    }

    // =========================================================================
    // 3. NHÀ XUẤT BẢN (/Admin/Publishers)
    // =========================================================================
    [HttpGet("Publishers")]
    public async Task<IActionResult> Publishers([FromQuery] BaseFilter filter, CancellationToken ct)
    {
        var paged = await masterService.GetPublishersPagedAsync(filter, ct);
        return View("Publishers", new PublisherPageViewModel
        {
            PagedItems = paged,
            Filter = filter
        });
    }

    [HttpPost("Publishers/Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePublisher(PublisherFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.CommonValidationError));
            return RedirectToAction(nameof(Publishers));
        }

        var result = await masterService.SavePublisherAsync(model, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Publishers));
    }

    [HttpPost("Publishers/Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePublisher(Guid id, CancellationToken ct)
    {
        var result = await masterService.DeletePublisherAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Publishers));
    }

    [HttpPost("Publishers/Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestorePublisher(Guid id, CancellationToken ct)
    {
        var result = await masterService.RestorePublisherAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Publishers), new { status = EntityStatus.Deleted });
    }

    [HttpPost("Publishers/HardDelete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDeletePublisher(Guid id, [FromForm] string? confirmCode, CancellationToken ct)
    {
        var result = await masterService.HardDeletePublisherAsync(id, confirmCode, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Publishers), new { status = EntityStatus.Deleted });
    }

    // =========================================================================
    // 4. VỊ TRÍ KỆ SÁCH (/Admin/Shelves)
    // =========================================================================
    [HttpGet("Shelves")]
    public async Task<IActionResult> Shelves([FromQuery] BaseFilter filter, CancellationToken ct)
    {
        var paged = await masterService.GetShelvesPagedAsync(filter, ct);
        return View("Shelves", new ShelfPageViewModel
        {
            PagedItems = paged,
            Filter = filter
        });
    }

    [HttpPost("Shelves/Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveShelf(ShelfFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.CommonValidationError));
            return RedirectToAction(nameof(Shelves));
        }

        var result = await masterService.SaveShelfAsync(model, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Shelves));
    }

    [HttpPost("Shelves/Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteShelf(Guid id, CancellationToken ct)
    {
        var result = await masterService.DeleteShelfAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Shelves));
    }

    [HttpPost("Shelves/Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreShelf(Guid id, CancellationToken ct)
    {
        var result = await masterService.RestoreShelfAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Shelves), new { status = EntityStatus.Deleted });
    }

    [HttpPost("Shelves/HardDelete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDeleteShelf(Guid id, [FromForm] string? confirmCode, CancellationToken ct)
    {
        var result = await masterService.HardDeleteShelfAsync(id, confirmCode, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Shelves), new { status = EntityStatus.Deleted });
    }
}

