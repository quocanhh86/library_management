using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Constant;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.AdminCatalog;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Librarian)]
[Route("Admin/BookCopies")]
public class AdminBookCopiesController(
    IBookService bookService,
    ICatalogMasterService masterService,
    IDateTimeProvider dateTimeProvider) : BaseController
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] AdminBookCopyFilter filter, CancellationToken ct)
    {
        var pagedCopies = await bookService.GetBookCopiesPagedAsync(filter, ct);
        var books = await masterService.GetActiveBooksSelectAsync(ct);
        var shelves = await masterService.GetActiveShelvesAsync(ct);

        var model = new AdminBookCopiesPageViewModel
        {
            PagedCopies = pagedCopies,
            Filter = filter,
            Books = books,
            Shelves = shelves,
            BatchForm = new BookCopyBatchCreateViewModel
            {
                BookId = filter.BookId ?? (books.Count > 0 ? books[0].Id : Guid.Empty),
                ShelfId = filter.ShelfId ?? (shelves.Count > 0 ? shelves[0].Id : Guid.Empty),
                Quantity = 2,
                Price = 120000m,
                AcquiredDate = dateTimeProvider.Today
            }
        };

        return View(model);
    }

    [HttpPost("CreateBatch")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBatch(BookCopyBatchCreateViewModel model, Guid? filterBookId, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.CommonValidationError));
            return RedirectToAction(nameof(Index), new { bookId = filterBookId });
        }

        var result = await bookService.CreateBookCopiesBatchAsync(model, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Index), new { bookId = filterBookId ?? model.BookId });
    }

    [HttpPost("Update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(BookCopyUpdateViewModel model, Guid? filterBookId, string? filterStatus, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.CommonValidationError));
            return RedirectToAction(nameof(Index), new { bookId = filterBookId, copyStatus = filterStatus });
        }

        var result = await bookService.UpdateBookCopyAsync(model, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Index), new { bookId = filterBookId, copyStatus = filterStatus });
    }

    [HttpPost("Dispose")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispose(BookCopyDisposeViewModel model, Guid? filterBookId, string? filterStatus, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.CommonValidationError));
            return RedirectToAction(nameof(Index), new { bookId = filterBookId, copyStatus = filterStatus });
        }

        var result = await bookService.DisposeOrMarkCopyAsync(model, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Index), new { bookId = filterBookId, copyStatus = filterStatus });
    }

    [HttpPost("Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, Guid? filterBookId, CancellationToken ct)
    {
        var result = await bookService.SoftDeleteBookCopyAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Index), new { bookId = filterBookId });
    }

    [HttpPost("Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(Guid id, Guid? filterBookId, CancellationToken ct)
    {
        var result = await bookService.RestoreBookCopyAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Index), new { bookId = filterBookId, status = EntityStatus.Deleted });
    }

    [HttpPost("HardDelete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDelete(Guid id, [FromForm] string? confirmCode, Guid? filterBookId, CancellationToken ct)
    {
        var result = await bookService.HardDeleteBookCopyAsync(id, confirmCode, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Index), new { bookId = filterBookId, status = EntityStatus.Deleted });
    }
}

