using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

public class BooksController(IBookService bookService) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? keyword,
        string? category,
        Guid? authorId,
        Guid? publisherId,
        int? year,
        bool available = false,
        int page = 1,
        CancellationToken ct = default)
    {
        var model = await bookService.GetPublicCatalogAsync(
            keyword,
            category,
            authorId,
            publisherId,
            year,
            available,
            page,
            ct);

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var book = await bookService.GetPublicBookDetailAsync(id, ct);
        if (book is null)
        {
            return NotFound();
        }

        return View(book);
    }

    /// <summary>Gợi ý khi gõ ở ô tìm kiếm (AJAX, trả PartialView các &lt;li&gt;).</summary>
    [HttpGet]
    public async Task<IActionResult> Suggest(string? keyword, CancellationToken ct)
    {
        var items = await bookService.SuggestAsync(keyword ?? string.Empty, 6, ct);
        return PartialView("_SearchSuggestions", items);
    }
}
