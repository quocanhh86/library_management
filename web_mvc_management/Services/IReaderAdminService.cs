using web_mvc_management.Common;
using web_mvc_management.Models.ViewModels.SystemAdmin;

namespace web_mvc_management.Services;

/// <summary>
/// Nghiệp vụ quản lý tài khoản & thẻ độc giả (/Admin/Readers).
/// </summary>
public interface IReaderAdminService
{
    Task<AdminReadersPageViewModel> GetPageAsync(ReaderFilter filter, CancellationToken ct = default);
    Task<ServiceResult> UpdateAsync(ReaderManageViewModel model, CancellationToken ct = default);
    Task<ServiceResult> SoftDeleteAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> RestoreAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> HardDeleteAsync(Guid id, string? confirmCode, CancellationToken ct = default);
}
