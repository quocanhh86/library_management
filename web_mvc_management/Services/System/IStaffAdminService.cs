using web_mvc_management.Common;
using web_mvc_management.Models.ViewModels.SystemAdmin;

namespace web_mvc_management.Services;

/// <summary>
/// Dịch vụ quản trị tài khoản Cán bộ & Phân quyền (/System/Staff).
/// </summary>
public interface IStaffAdminService
{
    Task<PagedResult<StaffListItemViewModel>> GetStaffPagedAsync(StaffFilter filter, CancellationToken ct = default);

    Task<ServiceResult> CreateStaffAsync(StaffCreateViewModel model, CancellationToken ct = default);

    Task<ServiceResult> UpdateStaffAsync(StaffEditViewModel model, CancellationToken ct = default);

    Task<ServiceResult> UnlockStaffAccountAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> SoftDeleteStaffAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> RestoreStaffAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> HardDeleteStaffAsync(Guid id, string? confirmCode, CancellationToken ct = default);
}
