using web_mvc_management.Common;
using web_mvc_management.Models.ViewModels.SystemAdmin;

namespace web_mvc_management.Services;

/// <summary>
/// Dịch vụ cấu hình Loại độc giả (/Admin/ReaderTypes).
/// </summary>
public interface IReaderTypeService
{
    Task<IReadOnlyList<ReaderTypeItemViewModel>> GetReaderTypesAsync(CancellationToken ct = default);

    Task<ServiceResult> SaveReaderTypeAsync(ReaderTypeFormViewModel model, CancellationToken ct = default);

    Task<ServiceResult> DeleteReaderTypeAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> RestoreReaderTypeAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> HardDeleteReaderTypeAsync(Guid id, string? confirmCode, CancellationToken ct = default);
}
