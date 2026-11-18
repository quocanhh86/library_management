using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Constant;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models;
using web_mvc_management.Models.ViewModels.Home;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

public class HomeController(
    IPublicCatalogService publicCatalogService,
    ICatalogLookupService catalogLookupService,
    ILibraryInfoService libraryInfoService,
    IReaderPortalService readerPortalService,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : BaseController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var readerId = User.IsInRole(AppRoles.Reader) ? userContext.CurrentUserId : null;
        return View(await BuildModelAsync(
            publicCatalogService,
            catalogLookupService,
            libraryInfoService,
            readerPortalService,
            dateTimeProvider,
            readerId,
            ct));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    /// <summary>
    /// Dựng model trang chủ. Tách ra để DevController xem trước giao diện độc giả đã đăng nhập.
    /// </summary>
    internal static async Task<HomeViewModel> BuildModelAsync(
        IPublicCatalogService publicCatalogService,
        ICatalogLookupService catalogLookupService,
        ILibraryInfoService libraryInfoService,
        IReaderPortalService readerPortalService,
        IDateTimeProvider dateTimeProvider,
        Guid? readerId,
        CancellationToken ct)
    {
        return new HomeViewModel
        {
            NewArrivals = await publicCatalogService.GetNewArrivalsAsync(26, ct),
            Topics = await catalogLookupService.GetTopicsAsync(ct),
            Authors = await catalogLookupService.GetActiveAuthorsAsync(ct),
            Publishers = await catalogLookupService.GetActivePublishersAsync(ct),
            Info = await libraryInfoService.GetAsync(ct),
            ReaderLoans = readerId is { } id ? await readerPortalService.GetHomeLoansAsync(id, ct) : null,
            Today = dateTimeProvider.Today
        };
    }
}
