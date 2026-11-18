using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Models.ViewModels.Circulation;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Librarian)]
public class CirculationController(
    ILoanDeskService loanDeskService,
    IReservationService reservationService,
    IFineService fineService) : BaseController
{
    [HttpGet("Circulation/Borrow")]
    public async Task<IActionResult> Borrow(string? card, CancellationToken ct) =>
        View(await loanDeskService.GetDeskAsync(returning: false, card, ct));

    [HttpPost("Circulation/Borrow")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Borrow(BorrowSubmitViewModel model, string? card, CancellationToken ct)
    {
        var result = ModelState.IsValid
            ? await loanDeskService.CreateBorrowSlipAsync(model, ct)
            : ServiceResult<Guid>.ValidationError(ErrorCode.CommonValidationError);

        if (result.Succeeded)
        {
            TempData["JustCreatedSlipId"] = result.Data.ToString();
        }

        return RedirectWithFlash(result, nameof(Borrow), routeValues: new { card });
    }

    [HttpGet("Circulation/Return")]
    public async Task<IActionResult> Return(string? card, CancellationToken ct) =>
        View(await loanDeskService.GetDeskAsync(returning: true, card, ct));

    [HttpPost("Circulation/Return")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(ReturnSubmitViewModel model, string? card, CancellationToken ct)
    {
        var result = ModelState.IsValid
            ? await loanDeskService.ReturnAsync(model, ct)
            : ServiceResult.ValidationError(ErrorCode.CommonValidationError);

        return RedirectWithFlash(result, nameof(Return), routeValues: new { card });
    }

    [HttpPost("Circulation/Renew")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Renew(Guid detailId, string? card, CancellationToken ct) =>
        RedirectWithFlash(await loanDeskService.RenewAsync(detailId, ct), nameof(Return), routeValues: new { card });

    // ===== Quản lý phiếu mượn & Lịch sử =====

    [HttpGet("Circulation/History")]
    public async Task<IActionResult> History([FromQuery] BorrowSlipFilter filter, CancellationToken ct) =>
        View(await loanDeskService.GetBorrowHistoryPageAsync(filter, ct));

    [HttpGet("Circulation/History/Detail/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var model = await loanDeskService.GetBorrowSlipDetailAsync(id, ct);
        if (model is null)
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.BorrowSlipNotFound));
            return RedirectToAction(nameof(History));
        }

        return View(model);
    }

    [HttpGet("Circulation/History/Print/{id:guid}")]
    public async Task<IActionResult> Print(Guid id, CancellationToken ct)
    {
        var model = await loanDeskService.GetBorrowSlipDetailAsync(id, ct);
        if (model is null)
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.BorrowSlipNotFound));
            return RedirectToAction(nameof(History));
        }

        return View(model);
    }

    [HttpPost("Circulation/History/Cancel/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, BorrowCancelViewModel model, CancellationToken ct)
    {
        var result = ModelState.IsValid
            ? await loanDeskService.CancelBorrowSlipAsync(id, model.Reason, ct)
            : ServiceResult.ValidationError(ErrorCode.BorrowSlipCancelReasonRequired);

        return RedirectWithFlash(result, nameof(History));
    }

    // ===== Đặt trước =====

    [HttpGet("Reservations")]
    public async Task<IActionResult> Reservations([FromQuery] ReservationFilter filter, CancellationToken ct) =>
        View(await reservationService.GetReservationsPageAsync(filter, ct));

    [HttpPost("Reservations/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReservation(ReservationCreateViewModel model, CancellationToken ct)
    {
        var result = ModelState.IsValid
            ? await reservationService.CreateReservationAsync(model, ct)
            : ServiceResult.ValidationError(ErrorCode.CommonValidationError);

        return RedirectWithFlash(result, nameof(Reservations));
    }

    [HttpPost("Reservations/Cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelReservation(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await reservationService.CancelReservationAsync(id, ct), nameof(Reservations));

    // ===== Thu phạt =====

    [HttpGet("Fines")]
    public async Task<IActionResult> Fines([FromQuery] FineFilter filter, CancellationToken ct) =>
        View(await fineService.GetFinesPageAsync(filter, ct));

    [HttpPost("Fines/Pay")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayFine(Guid id, string? paymentMethod, string? keyword, CancellationToken ct) =>
        RedirectWithFlash(await fineService.PayFineAsync(id, paymentMethod, ct), nameof(Fines), routeValues: new { keyword });

    [HttpPost("Fines/Waive")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WaiveFine(Guid id, string? note, string? keyword, CancellationToken ct) =>
        RedirectWithFlash(await fineService.WaiveFineAsync(id, note, ct), nameof(Fines), routeValues: new { keyword });
}
