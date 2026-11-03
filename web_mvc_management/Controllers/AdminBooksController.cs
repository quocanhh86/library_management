using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Constant;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.AdminCatalog;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Librarian)]
[Route("Admin/Books")]
public class AdminBooksController(
    IBookService bookService,
    ICatalogMasterService masterService,
    IDateTimeProvider dateTimeProvider) : BaseController
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] AdminBookFilter filter, CancellationToken ct)
    {
        var pagedBooks = await bookService.GetAdminBooksPagedAsync(filter, ct);
        var model = new AdminBooksPageViewModel
        {
            PagedBooks = pagedBooks,
            Filter = filter,
            Categories = await masterService.GetActiveCategoriesAsync(ct),
            Authors = await masterService.GetActiveAuthorsAsync(ct),
            Publishers = await masterService.GetActivePublishersAsync(ct)
        };

        return View(model);
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var model = new BookUpsertViewModel
        {
            PublicationYear = (short)dateTimeProvider.Today.Year,
            AcquiredDate = dateTimeProvider.Today,
            InitialCopyCount = 3,
            CopyPrice = 120000m
        };

        await PopulateSelectListsAsync(model, ct);
        if (model.Shelves.Count > 0)
        {
            model.ShelfId = model.Shelves[0].Id;
        }

        return View("Form", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookUpsertViewModel model, CancellationToken ct)
    {
        if (model.AuthorIds == null || model.AuthorIds.Count == 0)
        {
            ModelState.AddModelError(nameof(model.AuthorIds), "Vui lòng chọn ít nhất một tác giả.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateSelectListsAsync(model, ct);
            return View("Form", model);
        }

        var result = await bookService.CreateBookWithCopiesAsync(model, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            SetErrorMessage(result.Message);
            await PopulateSelectListsAsync(model, ct);
            return View("Form", model);
        }

        SetSuccessMessage(result.Message);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var model = await bookService.GetBookForEditAsync(id, ct);
        if (model == null)
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.BookNotFound));
            return RedirectToAction(nameof(Index));
        }

        await PopulateSelectListsAsync(model, ct);
        return View("Form", model);
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, BookUpsertViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (model.AuthorIds == null || model.AuthorIds.Count == 0)
        {
            ModelState.AddModelError(nameof(model.AuthorIds), "Vui lòng chọn ít nhất một tác giả.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateSelectListsAsync(model, ct);
            return View("Form", model);
        }

        var result = await bookService.UpdateBookAsync(model, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            SetErrorMessage(result.Message);
            await PopulateSelectListsAsync(model, ct);
            return View("Form", model);
        }

        SetSuccessMessage(result.Message);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await bookService.DeleteBookAsync(id, ct);
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
        var result = await bookService.RestoreBookAsync(id, ct);
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
        var result = await bookService.HardDeleteBookAsync(id, confirmCode, ct);
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

    private async Task PopulateSelectListsAsync(BookUpsertViewModel model, CancellationToken ct)
    {
        model.Categories = await masterService.GetActiveCategoriesAsync(ct);
        model.Authors = await masterService.GetActiveAuthorsAsync(ct);
        model.Publishers = await masterService.GetActivePublishersAsync(ct);
        model.Shelves = await masterService.GetActiveShelvesAsync(ct);

        if (model.Id.HasValue && model.Id.Value != Guid.Empty && model.ExistingImages.Count == 0)
        {
            var latest = await bookService.GetBookForEditAsync(model.Id.Value, ct);
            if (latest != null)
            {
                model.ExistingImages = latest.ExistingImages;
            }
        }
    }
}
