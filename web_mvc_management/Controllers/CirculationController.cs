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
            : ServiceResult.ValidationError(ErrorCode.CommonValidationError);
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
