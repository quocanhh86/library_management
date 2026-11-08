using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Reader;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

/// <summary>
/// Cổng Bạn Đọc Tự Phục Vụ (Client Reader Portal):
/// - Sách đang mượn & Thẻ thư viện (/Reader/Profile)
/// - Gia hạn sách trực tuyến (/Reader/Renew)
/// - Lịch sử mượn trả (/Reader/BorrowHistory)
/// - Tiền phạt cá nhân (/Reader/Fines)
/// - Sách đặt trước (/Reader/Reservations)
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
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        if (userContext.CurrentUserId is not { } readerId)
        {
            return RedirectToAction("Login", "Account");
        }

        var model = await readerPortalService.GetProfilePageAsync(readerId, ct);
        if (model is null)
        {
            return RedirectToAction("Login", "Account");
        }

        return View(model);
    }

    [HttpPost("Renew")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Renew(Guid detailId, CancellationToken ct)
    {
        if (userContext.CurrentUserId is not { } readerId)
        {
            return RedirectToAction("Login", "Account");
        }

        var result = await readerPortalService.RenewLoanAsync(readerId, detailId, ct);
        Flash(result);
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet("BorrowHistory")]
    public async Task<IActionResult> BorrowHistory([FromQuery] ReaderBorrowHistoryFilter filter, CancellationToken ct)
    {
        if (userContext.CurrentUserId is not { } readerId)
        {
            return RedirectToAction("Login", "Account");
        }

        var model = await readerPortalService.GetBorrowHistoryPageAsync(readerId, filter, ct);
        if (model is null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (IsAjaxRequest)
        {
            return PartialView("_BorrowHistoryTable", model);
        }

        return View(model);
    }

    [HttpGet("Fines")]
    public async Task<IActionResult> Fines([FromQuery] ReaderFineFilter filter, CancellationToken ct)
    {
        if (userContext.CurrentUserId is not { } readerId)
        {
            return RedirectToAction("Login", "Account");
        }

        var model = await readerPortalService.GetFinesPageAsync(readerId, filter, ct);
        if (model is null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (IsAjaxRequest)
        {
            return PartialView("_FinesTable", model);
        }

        return View(model);
    }

    [HttpGet("Reservations")]
    public async Task<IActionResult> Reservations(CancellationToken ct)
    {
        if (userContext.CurrentUserId is not { } readerId)
        {
            return RedirectToAction("Login", "Account");
        }

        var model = await readerPortalService.GetReservationsPageAsync(readerId, ct);
        if (model is null)
        {
            return RedirectToAction("Login", "Account");
        }

        return View(model);
    }

    [HttpPost("Reservations/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReservation(Guid bookId, string? returnUrl, CancellationToken ct)
    {
        if (userContext.CurrentUserId is not { } readerId)
        {
            return RedirectToAction("Login", "Account");
        }

        var result = await readerPortalService.CreateReservationAsync(readerId, bookId, ct);
        Flash(result);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Reservations));
    }

    [HttpPost("Reservations/Cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelReservation(Guid id, CancellationToken ct)
    {
        if (userContext.CurrentUserId is not { } readerId)
        {
            return RedirectToAction("Login", "Account");
        }

        var result = await readerPortalService.CancelReservationAsync(readerId, id, ct);
        Flash(result);
        return RedirectToAction(nameof(Reservations));
    }

    private void Flash(ServiceResult result)
    {
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }
    }
}
