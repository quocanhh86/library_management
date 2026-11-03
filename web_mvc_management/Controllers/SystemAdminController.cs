using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Constant;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class SystemAdminController(
    ISystemAdminService systemAdminService,
    IUserContext userContext) : BaseController
{
    // =========================================================================
    // 1. QUẢN LÝ TÀI KHOẢN CÁN BỘ & PHÂN QUYỀN (/System/Staff)
    // =========================================================================
    [HttpGet("System/Staff")]
    public async Task<IActionResult> Staff([FromQuery] StaffFilter filter, CancellationToken ct)
    {
        var paged = await systemAdminService.GetStaffPagedAsync(filter, ct);
        return View("Staff", new StaffPageViewModel
        {
            PagedStaff = paged,
            Filter = filter,
            CurrentUserId = userContext.CurrentUserId
        });
    }

    [HttpPost("System/Staff/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStaff(StaffCreateViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.CommonValidationError));
            return RedirectToAction(nameof(Staff));
        }

        var result = await systemAdminService.CreateStaffAsync(model, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Staff));
    }

    [HttpPost("System/Staff/Update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStaff(StaffEditViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.CommonValidationError));
            return RedirectToAction(nameof(Staff));
        }

        var result = await systemAdminService.UpdateStaffAsync(model, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Staff));
    }

    [HttpPost("System/Staff/Unlock/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlockStaff(Guid id, CancellationToken ct)
    {
        var result = await systemAdminService.UnlockStaffAccountAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Staff));
    }

    [HttpPost("System/Staff/Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStaff(Guid id, CancellationToken ct)
    {
        var result = await systemAdminService.SoftDeleteStaffAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Staff));
    }

    [HttpPost("System/Staff/Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreStaff(Guid id, CancellationToken ct)
    {
        var result = await systemAdminService.RestoreStaffAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Staff), new { status = EntityStatus.Deleted });
    }

    [HttpPost("System/Staff/HardDelete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDeleteStaff(Guid id, [FromForm] string? confirmCode, CancellationToken ct)
    {
        var result = await systemAdminService.HardDeleteStaffAsync(id, confirmCode, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Staff), new { status = EntityStatus.Deleted });
    }

    // =========================================================================
    // 2. CẤU HÌNH LOẠI ĐỘC GIẢ: SỐ SÁCH TỐI ĐA & SỐ NGÀY MƯỢN (/Admin/ReaderTypes)
    // =========================================================================
    [HttpGet("Admin/ReaderTypes")]
    public async Task<IActionResult> ReaderTypes(CancellationToken ct)
    {
        var items = await systemAdminService.GetReaderTypesAsync(ct);
        return View("ReaderTypes", new ReaderTypesPageViewModel
        {
            Items = items
        });
    }

    [HttpPost("Admin/ReaderTypes/Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReaderType(ReaderTypeFormViewModel model, string? returnPage, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.CommonValidationError));
            return string.Equals(returnPage, "Settings", StringComparison.OrdinalIgnoreCase)
                ? RedirectToAction(nameof(Settings))
                : RedirectToAction(nameof(ReaderTypes));
        }

        var result = await systemAdminService.SaveReaderTypeAsync(model, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return string.Equals(returnPage, "Settings", StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction(nameof(Settings))
            : RedirectToAction(nameof(ReaderTypes));
    }

    [HttpPost("Admin/ReaderTypes/Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReaderType(Guid id, CancellationToken ct)
    {
        var result = await systemAdminService.DeleteReaderTypeAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(ReaderTypes));
    }

    [HttpPost("Admin/ReaderTypes/Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreReaderType(Guid id, CancellationToken ct)
    {
        var result = await systemAdminService.RestoreReaderTypeAsync(id, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(ReaderTypes));
    }

    [HttpPost("Admin/ReaderTypes/HardDelete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDeleteReaderType(Guid id, [FromForm] string? confirmCode, CancellationToken ct)
    {
        var result = await systemAdminService.HardDeleteReaderTypeAsync(id, confirmCode, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(ReaderTypes));
    }

    // =========================================================================
    // 3. CẤU HÌNH QUY ĐỊNH MƯỢN TRẢ & MỨC PHẠT (/System/Settings)
    // =========================================================================
    [HttpGet("System/Settings")]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        var settingForm = await systemAdminService.GetLibrarySettingAsync(ct);
        var readerTypes = await systemAdminService.GetReaderTypesAsync(ct);

        return View("Settings", new SystemSettingsPageViewModel
        {
            SettingForm = settingForm,
            ReaderTypes = readerTypes
        });
    }

    [HttpPost("System/Settings/Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings([Bind(Prefix = "SettingForm")] LibrarySettingFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var readerTypes = await systemAdminService.GetReaderTypesAsync(ct);
            SetErrorMessage(ErrorMessages.Get(ErrorCode.CommonValidationError));
            return View("Settings", new SystemSettingsPageViewModel
            {
                SettingForm = model,
                ReaderTypes = readerTypes
            });
        }

        var result = await systemAdminService.SaveLibrarySettingAsync(model, ct);
        if (result.Succeeded)
        {
            SetSuccessMessage(result.Message);
        }
        else
        {
            SetErrorMessage(result.Message);
        }

        return RedirectToAction(nameof(Settings));
    }
}

