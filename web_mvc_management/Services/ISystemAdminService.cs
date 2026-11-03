using web_mvc_management.Common;
using web_mvc_management.Models.ViewModels.SystemAdmin;

namespace web_mvc_management.Services;

public interface ISystemAdminService
{
    // 1. Quản lý Tài khoản Cán bộ & Phân quyền (/System/Staff)
    Task<PagedResult<StaffListItemViewModel>> GetStaffPagedAsync(StaffFilter filter, CancellationToken ct = default);
    Task<ServiceResult> CreateStaffAsync(StaffCreateViewModel model, CancellationToken ct = default);
    Task<ServiceResult> UpdateStaffAsync(StaffEditViewModel model, CancellationToken ct = default);
    Task<ServiceResult> UnlockStaffAccountAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> SoftDeleteStaffAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> RestoreStaffAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> HardDeleteStaffAsync(Guid id, string? confirmCode, CancellationToken ct = default);

    // 2. Cấu hình Loại độc giả: Số sách tối đa, Số ngày mượn (/Admin/ReaderTypes)
    Task<IReadOnlyList<ReaderTypeItemViewModel>> GetReaderTypesAsync(CancellationToken ct = default);
    Task<ServiceResult> SaveReaderTypeAsync(ReaderTypeFormViewModel model, CancellationToken ct = default);
    Task<ServiceResult> DeleteReaderTypeAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> RestoreReaderTypeAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> HardDeleteReaderTypeAsync(Guid id, string? confirmCode, CancellationToken ct = default);

    // 3. Cấu hình Quy định Mượn trả & Mức phạt (/System/Settings)
    Task<LibrarySettingFormViewModel> GetLibrarySettingAsync(CancellationToken ct = default);
    Task<ServiceResult> SaveLibrarySettingAsync(LibrarySettingFormViewModel model, CancellationToken ct = default);
}

