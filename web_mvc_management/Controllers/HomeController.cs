using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models;
using web_mvc_management.Models.ViewModels.Home;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

public class HomeController(
    IBookService bookService,
    ILibraryInfoService libraryInfoService,
    IDateTimeProvider dateTimeProvider) : BaseController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        return View(await BuildModelAsync(bookService, libraryInfoService, dateTimeProvider, ct));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    /// <summary>
    /// Dựng model trang chủ. Tách ra để DevController xem trước giao diện độc giả đã đăng nhập.
    /// </summary>
    internal static async Task<HomeViewModel> BuildModelAsync(
        IBookService bookService,
        ILibraryInfoService libraryInfoService,
        IDateTimeProvider dateTimeProvider,
        CancellationToken ct)
    {
        return new HomeViewModel
        {
            NewArrivals = await bookService.GetNewArrivalsAsync(12, ct),
            Topics = await bookService.GetTopicsAsync(ct),
            Authors = await bookService.GetActiveAuthorsSelectAsync(ct),
            Publishers = await bookService.GetActivePublishersSelectAsync(ct),
            Info = await libraryInfoService.GetAsync(ct),
            ReaderLoans = null,
            Today = dateTimeProvider.Today
        };
    }
}
