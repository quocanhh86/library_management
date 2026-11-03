using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Services;
using web_mvc_management.Models.ViewModels.Catalog;
using web_mvc_management.Util;

namespace web_mvc_management.Controllers;

public class BooksController(IBookService bookService) : BaseController
{
    // Presentation over the existing UI catalog service; no persistence changes.
    [HttpGet]
    public async Task<IActionResult> Index(string? keyword, string? category, int? year, bool available = false, int page = 1, CancellationToken ct = default)
    {
        var books = await bookService.GetNewArrivalsAsync(int.MaxValue, ct);
        var term = StringUtil.RemoveVietnameseSigns(keyword?.Trim() ?? string.Empty).ToLowerInvariant();
        var filtered = books.Where(b =>
            (term.Length == 0 || StringUtil.RemoveVietnameseSigns($"{b.Title} {b.Authors} {b.Isbn}").ToLowerInvariant().Contains(term)) &&
            (string.IsNullOrEmpty(category) || b.CategoryKey == category) &&
            (year == null || b.PublicationYear == year) &&
            (!available || b.AvailableCopies > 0)).ToList();
        const int pageSize = 10;
        var pages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
        page = Math.Clamp(page, 1, pages);
        return View(new CatalogViewModel
        {
            Books = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            Topics = await bookService.GetTopicsAsync(ct),
            Keyword = keyword, Category = category, Year = year, Available = available,
            Page = page, TotalCount = filtered.Count, TotalPages = pages
        });
    }

    [HttpGet]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var books = await bookService.GetNewArrivalsAsync(int.MaxValue, ct);
        var book = books.FirstOrDefault(b => b.Id == id);
        if (book is null) return NotFound();
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
