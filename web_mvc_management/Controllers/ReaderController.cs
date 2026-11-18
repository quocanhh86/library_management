using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Constant;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Reader;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

/// <summary>
/// Cổng Bạn Đọc Tự Phục Vụ (Client Reader Portal).
/// </summary>
[Authorize(Roles = AppRoles.Reader)]
[Route("Reader")]
public class ReaderController(
    IReaderPortalService readerPortalService,
    IUserContext userContext) : BaseController
{
    [HttpGet("")]
    public IActionResult Index() => RedirectToAction(nameof(Profile));

    [HttpGet("Profile")]
    public async Task<IActionResult> Profile(CancellationToken ct) =>
        await LoadPortalViewAsync(readerId => readerPortalService.GetProfilePageAsync(readerId, ct));

    [HttpPost("Renew")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Renew(Guid detailId, CancellationToken ct)
    {
        if (userContext.CurrentUserId is not { } readerId)
        {
            return RedirectToAction("Login", "Account");
        }

        return RedirectWithFlash(await readerPortalService.RenewLoanAsync(readerId, detailId, ct), nameof(Profile));
    }

    [HttpGet("BorrowHistory")]
    public async Task<IActionResult> BorrowHistory([FromQuery] ReaderBorrowHistoryFilter filter, CancellationToken ct) =>
        await LoadPortalViewAsync(
            readerId => readerPortalService.GetBorrowHistoryPageAsync(readerId, filter, ct),
            partialViewOnAjax: "_BorrowHistoryTable");

    [HttpGet("Fines")]
    public async Task<IActionResult> Fines([FromQuery] ReaderFineFilter filter, CancellationToken ct) =>
        await LoadPortalViewAsync(
            readerId => readerPortalService.GetFinesPageAsync(readerId, filter, ct),
            partialViewOnAjax: "_FinesTable");

    [HttpGet("Reservations")]
    public async Task<IActionResult> Reservations(CancellationToken ct) =>
        await LoadPortalViewAsync(readerId => readerPortalService.GetReservationsPageAsync(readerId, ct));

    [HttpPost("Reservations/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReservation(Guid bookId, string? returnUrl, CancellationToken ct)
    {
        if (userContext.CurrentUserId is not { } readerId)
        {
            return RedirectToAction("Login", "Account");
        }

        FlashResult(await readerPortalService.CreateReservationAsync(readerId, bookId, ct));

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Reservations));
    }

    [HttpPost("Reservations/Cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelReservation(Guid reservationId, Guid id, CancellationToken ct)
    {
        if (userContext.CurrentUserId is not { } readerId)
        {
            return RedirectToAction("Login", "Account");
        }

        var targetId = reservationId != Guid.Empty ? reservationId : id;
        return RedirectWithFlash(await readerPortalService.CancelReservationAsync(readerId, targetId, ct), nameof(Reservations));
    }

    private async Task<IActionResult> LoadPortalViewAsync(
        Func<Guid, Task<ReaderPortalPageViewModel?>> loader,
        string? partialViewOnAjax = null)
    {
        if (userContext.CurrentUserId is not { } readerId)
        {
            return RedirectToAction("Login", "Account");
        }

        var model = await loader(readerId);
        if (model is null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (partialViewOnAjax != null && IsAjaxRequest)
        {
            return PartialView(partialViewOnAjax, model);
        }

        return View(model);
    }
}
