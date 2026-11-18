using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.AdminCatalog;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Librarian)]
[Route("Admin/BookCopies")]
public class AdminBookCopiesController(
    IBookCopyService bookCopyService,
    ICatalogLookupService catalogLookupService,
    IDateTimeProvider dateTimeProvider) : BaseController
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] AdminBookCopyFilter filter, CancellationToken ct)
    {
        var pagedCopies = await bookCopyService.GetBookCopiesPagedAsync(filter, ct);
        var books = await catalogLookupService.GetActiveBooksSelectAsync(ct);
        var shelves = await catalogLookupService.GetActiveShelvesAsync(ct);

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
            return RedirectWithFlash(
                ServiceResult.ValidationError(ErrorCode.CommonValidationError),
                nameof(Index),
                routeValues: new { bookId = filterBookId });
        }

        return RedirectWithFlash(
            await bookCopyService.CreateBookCopiesBatchAsync(model, ct),
            nameof(Index),
            routeValues: new { bookId = filterBookId ?? model.BookId });
    }

    [HttpPost("Update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(BookCopyUpdateViewModel model, Guid? filterBookId, string? filterStatus, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return RedirectWithFlash(
                ServiceResult.ValidationError(ErrorCode.CommonValidationError),
                nameof(Index),
                routeValues: new { bookId = filterBookId, copyStatus = filterStatus });
        }

        return RedirectWithFlash(
            await bookCopyService.UpdateBookCopyAsync(model, ct),
            nameof(Index),
            routeValues: new { bookId = filterBookId, copyStatus = filterStatus });
    }

    [HttpPost("Dispose")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispose(BookCopyDisposeViewModel model, Guid? filterBookId, string? filterStatus, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return RedirectWithFlash(
                ServiceResult.ValidationError(ErrorCode.CommonValidationError),
                nameof(Index),
                routeValues: new { bookId = filterBookId, copyStatus = filterStatus });
        }

        return RedirectWithFlash(
            await bookCopyService.DisposeOrMarkCopyAsync(model, ct),
            nameof(Index),
            routeValues: new { bookId = filterBookId, copyStatus = filterStatus });
    }

    [HttpPost("Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, Guid? filterBookId, CancellationToken ct) =>
        RedirectWithFlash(
            await bookCopyService.SoftDeleteBookCopyAsync(id, ct),
            nameof(Index),
            routeValues: new { bookId = filterBookId });

    [HttpPost("Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(Guid id, Guid? filterBookId, CancellationToken ct) =>
        RedirectWithFlash(
            await bookCopyService.RestoreBookCopyAsync(id, ct),
            nameof(Index),
            routeValues: new { bookId = filterBookId, status = EntityStatus.Deleted });

    [HttpPost("HardDelete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDelete(Guid id, [FromForm] string? confirmCode, Guid? filterBookId, CancellationToken ct) =>
        RedirectWithFlash(
            await bookCopyService.HardDeleteBookCopyAsync(id, confirmCode, ct),
            nameof(Index),
            routeValues: new { bookId = filterBookId, status = EntityStatus.Deleted });
}
