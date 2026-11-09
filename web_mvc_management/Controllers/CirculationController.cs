using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Models.ViewModels.Circulation;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Librarian)]
public class CirculationController(ICirculationService circulationService) : BaseController
{
    [HttpGet("Circulation/Borrow")]
    public async Task<IActionResult> Borrow(string? card, CancellationToken ct)
    {
        return View(await circulationService.GetDeskAsync(false, card, ct));
    }

    [HttpPost("Circulation/Borrow")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Borrow(BorrowSubmitViewModel model, string? card, CancellationToken ct)
    {
        var result = ModelState.IsValid
            ? await circulationService.CreateBorrowSlipAsync(model, ct)
            : ServiceResult<Guid>.ValidationError(ErrorCode.CommonValidationError);

        if (result.Succeeded)
        {
            TempData["JustCreatedSlipId"] = result.Data.ToString();
        }

        Flash(result);
        return RedirectToAction(nameof(Borrow), new { card });
    }

    [HttpGet("Circulation/Return")]
    public async Task<IActionResult> Return(string? card, CancellationToken ct)
    {
        return View(await circulationService.GetDeskAsync(true, card, ct));
    }

    [HttpPost("Circulation/Return")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(ReturnSubmitViewModel model, string? card, CancellationToken ct)
    {
        var result = ModelState.IsValid
            ? await circulationService.ReturnAsync(model, ct)
            : ServiceResult.ValidationError(ErrorCode.CommonValidationError);
        Flash(result);
        return RedirectToAction(nameof(Return), new { card });
    }

    [HttpPost("Circulation/Renew")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Renew(Guid detailId, string? card, CancellationToken ct)
    {
        Flash(await circulationService.RenewAsync(detailId, ct));
        return RedirectToAction(nameof(Return), new { card });
    }

    // ===== Quản lý phiếu mượn & Lịch sử =====

    [HttpGet("Circulation/History")]
    public async Task<IActionResult> History([FromQuery] BorrowSlipFilter filter, CancellationToken ct)
    {
        return View(await circulationService.GetBorrowHistoryPageAsync(filter, ct));
    }

    [HttpGet("Circulation/History/Detail/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var model = await circulationService.GetBorrowSlipDetailAsync(id, ct);
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
        var model = await circulationService.GetBorrowSlipDetailAsync(id, ct);
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
            ? await circulationService.CancelBorrowSlipAsync(id, model.Reason, ct)
            : ServiceResult.ValidationError(ErrorCode.BorrowSlipCancelReasonRequired);
        Flash(result);
        return RedirectToAction(nameof(History));
    }

    // ===== Đặt trước =====

    [HttpGet("Reservations")]
    public async Task<IActionResult> Reservations([FromQuery] ReservationFilter filter, CancellationToken ct)
    {
        return View(await circulationService.GetReservationsPageAsync(filter, ct));
    }

    [HttpPost("Reservations/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReservation(ReservationCreateViewModel model, CancellationToken ct)
    {
        var result = ModelState.IsValid
            ? await circulationService.CreateReservationAsync(model, ct)
            : ServiceResult.ValidationError(ErrorCode.CommonValidationError);
        Flash(result);
        return RedirectToAction(nameof(Reservations));
    }

    [HttpPost("Reservations/Cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelReservation(Guid id, CancellationToken ct)
    {
        Flash(await circulationService.CancelReservationAsync(id, ct));
        return RedirectToAction(nameof(Reservations));
    }

    // ===== Thu phạt =====

    [HttpGet("Fines")]
    public async Task<IActionResult> Fines([FromQuery] FineFilter filter, CancellationToken ct)
    {
        return View(await circulationService.GetFinesPageAsync(filter, ct));
    }

    [HttpPost("Fines/Pay")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayFine(Guid id, string? paymentMethod, string? keyword, CancellationToken ct)
    {
        Flash(await circulationService.PayFineAsync(id, paymentMethod, ct));
        return RedirectToAction(nameof(Fines), new { keyword });
    }

    [HttpPost("Fines/Waive")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WaiveFine(Guid id, string? note, string? keyword, CancellationToken ct)
    {
        Flash(await circulationService.WaiveFineAsync(id, note, ct));
        return RedirectToAction(nameof(Fines), new { keyword });
    }

    private void Flash(ServiceResult result)
    {
        if (result.Succeeded) SetSuccessMessage(result.Message);
        else SetErrorMessage(result.Message);
    }
}
