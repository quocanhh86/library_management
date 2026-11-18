using web_mvc_management.Common;
using web_mvc_management.Models.ViewModels.SystemAdmin;

namespace web_mvc_management.Services;

/// <summary>
/// Dịch vụ cấu hình quy định mượn trả &amp; mức phạt (/System/Settings).
/// </summary>
public interface ILibrarySettingService
{
    Task<LibrarySettingFormViewModel> GetLibrarySettingAsync(CancellationToken ct = default);

    Task<ServiceResult> SaveLibrarySettingAsync(LibrarySettingFormViewModel model, CancellationToken ct = default);
}
