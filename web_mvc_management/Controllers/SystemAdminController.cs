using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Librarian)]
public class SystemAdminController(
    IStaffAdminService staffAdminService,
    IReaderTypeService readerTypeService,
    ILibrarySettingService librarySettingService,
    IDashboardService dashboardService,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : BaseController
{
    // =========================================================================
    // 1. QUẢN LÝ TÀI KHOẢN CÁN BỘ & PHÂN QUYỀN (/System/Staff)
    // =========================================================================
    [HttpGet("System/Staff")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Staff([FromQuery] StaffFilter filter, CancellationToken ct) =>
        View("Staff", new StaffPageViewModel
        {
            PagedStaff = await staffAdminService.GetStaffPagedAsync(filter, ct),
            Filter = filter,
            CurrentUserId = userContext.CurrentUserId
        });

    [HttpPost("System/Staff/Create")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStaff(StaffCreateViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return RedirectWithFlash(ServiceResult.ValidationError(ErrorCode.CommonValidationError), nameof(Staff));
        }

        return RedirectWithFlash(await staffAdminService.CreateStaffAsync(model, ct), nameof(Staff));
    }

    [HttpPost("System/Staff/Update")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStaff(StaffEditViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return RedirectWithFlash(ServiceResult.ValidationError(ErrorCode.CommonValidationError), nameof(Staff));
        }

        return RedirectWithFlash(await staffAdminService.UpdateStaffAsync(model, ct), nameof(Staff));
    }

    [HttpPost("System/Staff/Unlock/{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlockStaff(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await staffAdminService.UnlockStaffAccountAsync(id, ct), nameof(Staff));

    [HttpPost("System/Staff/Delete/{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStaff(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await staffAdminService.SoftDeleteStaffAsync(id, ct), nameof(Staff));

    [HttpPost("System/Staff/Restore/{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreStaff(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await staffAdminService.RestoreStaffAsync(id, ct), nameof(Staff), routeValues: new { status = EntityStatus.Deleted });

    [HttpPost("System/Staff/HardDelete/{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDeleteStaff(Guid id, [FromForm] string? confirmCode, CancellationToken ct) =>
        RedirectWithFlash(await staffAdminService.HardDeleteStaffAsync(id, confirmCode, ct), nameof(Staff), routeValues: new { status = EntityStatus.Deleted });

    // =========================================================================
    // 2. CẤU HÌNH LOẠI ĐỘC GIẢ (/Admin/ReaderTypes)
    // =========================================================================
    [HttpGet("Admin/ReaderTypes")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> ReaderTypes(CancellationToken ct) =>
        View("ReaderTypes", new ReaderTypesPageViewModel
        {
            Items = await readerTypeService.GetReaderTypesAsync(ct)
        });

    [HttpPost("Admin/ReaderTypes/Save")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReaderType(ReaderTypeFormViewModel model, string? returnPage, CancellationToken ct)
    {
        var targetAction = string.Equals(returnPage, "Settings", StringComparison.OrdinalIgnoreCase)
            ? nameof(Settings)
            : nameof(ReaderTypes);

        if (!ModelState.IsValid)
        {
            return RedirectWithFlash(ServiceResult.ValidationError(ErrorCode.CommonValidationError), targetAction);
        }

        return RedirectWithFlash(await readerTypeService.SaveReaderTypeAsync(model, ct), targetAction);
    }

    [HttpPost("Admin/ReaderTypes/Delete/{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReaderType(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await readerTypeService.DeleteReaderTypeAsync(id, ct), nameof(ReaderTypes));

    [HttpPost("Admin/ReaderTypes/Restore/{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreReaderType(Guid id, CancellationToken ct) =>
        RedirectWithFlash(await readerTypeService.RestoreReaderTypeAsync(id, ct), nameof(ReaderTypes));

    [HttpPost("Admin/ReaderTypes/HardDelete/{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDeleteReaderType(Guid id, [FromForm] string? confirmCode, CancellationToken ct) =>
        RedirectWithFlash(await readerTypeService.HardDeleteReaderTypeAsync(id, confirmCode, ct), nameof(ReaderTypes));

    // =========================================================================
    // 3. CẤU HÌNH QUY ĐỊNH MƯỢN TRẢ & MỨC PHẠT (/System/Settings)
    // =========================================================================
    [HttpGet("System/Settings")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        var allTypes = await readerTypeService.GetReaderTypesAsync(ct);
        return View("Settings", new SystemSettingsPageViewModel
        {
            SettingForm = await librarySettingService.GetLibrarySettingAsync(ct),
            ReaderTypes = allTypes.Where(rt => rt.Status != EntityStatus.Deleted).ToList()
        });
    }

    [HttpPost("System/Settings/Save")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings([Bind(Prefix = "SettingForm")] LibrarySettingFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.CommonValidationError));
            var allTypes = await readerTypeService.GetReaderTypesAsync(ct);
            return View("Settings", new SystemSettingsPageViewModel
            {
                SettingForm = model,
                ReaderTypes = allTypes.Where(rt => rt.Status != EntityStatus.Deleted).ToList()
            });
        }

        return RedirectWithFlash(await librarySettingService.SaveLibrarySettingAsync(model, ct), nameof(Settings));
    }

    // =========================================================================
    // 4. TỔNG QUAN THỐNG KÊ (/Admin/Dashboard)
    // =========================================================================
    [HttpGet("Admin/Dashboard")]
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Librarian)]
    public async Task<IActionResult> Dashboard(int? year, CancellationToken ct)
    {
        var currentYear = dateTimeProvider.Today.Year;
        var selectedYear = year is >= 2000 and <= 2100 ? year.Value : currentYear;
        return View("Dashboard", await dashboardService.GetDashboardAsync(selectedYear, ct));
    }
}
