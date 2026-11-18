using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Extensions;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Repositories;
using web_mvc_management.Util;

namespace web_mvc_management.Services;

public class StaffAdminService(
    IUnitOfWork unitOfWork,
    IUserContext userContext) : IStaffAdminService
{
    public async Task<PagedResult<StaffListItemViewModel>> GetStaffPagedAsync(
        StaffFilter filter,
        CancellationToken ct = default)
    {
        var query = unitOfWork.Staff.GetQueryable(asNoTracking: true);

        if (filter.Status == EntityStatus.Deleted)
        {
            query = query.Where(s => s.Status == EntityStatus.Deleted);
        }
        else
        {
            query = query.Where(s => s.Status != EntityStatus.Deleted);
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                query = query.Where(s => s.Status == filter.Status);
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.ToLower();
            query = query.Where(s =>
                s.FullName.ToLower().Contains(kw) ||
                s.Email.ToLower().Contains(kw) ||
                s.Username.ToLower().Contains(kw) ||
                (s.Phone != null && s.Phone.Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Role))
        {
            query = query.Where(s => s.Role == filter.Role);
        }

        return await query
            .OrderBy(s => s.Role)
            .ThenBy(s => s.FullName)
            .Select(s => new StaffListItemViewModel
            {
                Id = s.Id,
                Username = s.Username,
                Email = s.Email,
                FullName = s.FullName,
                Phone = s.Phone,
                Role = s.Role,
                Status = s.Status,
                LastLoginAt = s.LastLoginAt,
                LockedUntil = s.LockedUntil,
                CreatedAt = s.CreatedAt
            })
            .ToPagedResultAsync(filter, ct);
    }

    public Task<ServiceResult> CreateStaffAsync(StaffCreateViewModel model, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var usernameTrim = model.Username.Trim().ToLowerInvariant();
            var emailTrim = model.Email.Trim().ToLowerInvariant();

            if (await unitOfWork.Staff.ExistsAsync(s => s.Username.ToLower() == usernameTrim, ct))
            {
                return ServiceResult.ValidationError(ErrorCode.StaffUsernameAlreadyExists);
            }

            if (await unitOfWork.Staff.ExistsAsync(s => s.Email.ToLower() == emailTrim, ct))
            {
                return ServiceResult.ValidationError(ErrorCode.StaffEmailAlreadyExists);
            }

            var role = model.Role == AppRoles.Admin ? AppRoles.Admin : AppRoles.Librarian;
            var staff = new Staff
            {
                Id = Guid.NewGuid(),
                Username = usernameTrim,
                Email = emailTrim,
                FullName = model.FullName.Trim(),
                Phone = model.Phone?.Trim(),
                Role = role,
                PasswordHash = PasswordHasher.HashPassword(model.Password),
                SecurityStamp = Guid.NewGuid().ToString("N"),
                Status = EntityStatus.Active
            };

            await unitOfWork.Staff.AddAsync(staff, ct);
            var roleLabel = role == AppRoles.Admin ? "Quản trị viên" : "Thủ thư";
            return ServiceResult.Success($"Đã tạo tài khoản cán bộ \"{staff.FullName}\" ({roleLabel}).");
        }, cancellationToken: ct);

    public Task<ServiceResult> UpdateStaffAsync(StaffEditViewModel model, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var staff = await unitOfWork.Staff.FirstOrDefaultAsync(
                s => s.Id == model.Id && s.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (staff == null)
            {
                return ServiceResult.Failure(ErrorCode.AuthStaffNotFound);
            }

            var emailTrim = model.Email.Trim().ToLowerInvariant();
            if (await unitOfWork.Staff.ExistsAsync(s => s.Email.ToLower() == emailTrim && s.Id != staff.Id, ct))
            {
                return ServiceResult.ValidationError(ErrorCode.StaffEmailAlreadyExists);
            }

            var targetRole = model.Role == AppRoles.Admin ? AppRoles.Admin : AppRoles.Librarian;
            var normalizedStatus = LibraryConstants.NormalizeActiveOrInactive(model.Status);

            // Bảo vệ không cho Admin tự khóa hoặc tự hạ quyền chính mình
            if (userContext.CurrentUserId == staff.Id &&
                (targetRole != staff.Role || normalizedStatus != EntityStatus.Active))
            {
                return ServiceResult.Failure(ErrorCode.StaffCannotModifySelf);
            }

            // Bảo vệ không làm mất Admin hoạt động cuối cùng của hệ thống
            if (staff.Role == AppRoles.Admin &&
                (targetRole != AppRoles.Admin || normalizedStatus != EntityStatus.Active))
            {
                var hasOtherActiveAdmin = await unitOfWork.Staff.ExistsAsync(
                    s => s.Id != staff.Id && s.Role == AppRoles.Admin && s.Status == EntityStatus.Active,
                    ct);
                if (!hasOtherActiveAdmin)
                {
                    return ServiceResult.Failure(
                        ErrorCode.CommonRecordInUse,
                        "Hệ thống bắt buộc phải có ít nhất một Quản trị viên (ADMIN) đang hoạt động.");
                }
            }

            var securitySensitiveChanged = staff.Role != targetRole ||
                                           staff.Status != normalizedStatus ||
                                           !string.IsNullOrWhiteSpace(model.NewPassword);

            staff.FullName = model.FullName.Trim();
            staff.Email = emailTrim;
            staff.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
            staff.Role = targetRole;
            staff.Status = normalizedStatus;

            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                staff.PasswordHash = PasswordHasher.HashPassword(model.NewPassword);
            }

            if (securitySensitiveChanged)
            {
                staff.SecurityStamp = Guid.NewGuid().ToString("N");
            }

            return ServiceResult.Success($"Đã cập nhật tài khoản cán bộ \"{staff.FullName}\".");
        }, cancellationToken: ct);

    public Task<ServiceResult> UnlockStaffAccountAsync(Guid id, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var staff = await unitOfWork.Staff.FirstOrDefaultAsync(
                s => s.Id == id && s.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (staff == null)
            {
                return ServiceResult.Failure(ErrorCode.AuthStaffNotFound);
            }

            staff.FailedLoginAttempts = 0;
            staff.LockedUntil = null;
            staff.Status = EntityStatus.Active;
            return ServiceResult.Success($"Đã mở khóa tài khoản cán bộ \"{staff.FullName}\".");
        }, cancellationToken: ct);

    public Task<ServiceResult> SoftDeleteStaffAsync(Guid id, CancellationToken ct = default)
    {
        if (userContext.CurrentUserId == id)
        {
            return Task.FromResult(ServiceResult.Failure(ErrorCode.StaffCannotModifySelf));
        }

        return unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var staff = await unitOfWork.Staff.FirstOrDefaultAsync(
                s => s.Id == id && s.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (staff == null)
            {
                return ServiceResult.Failure(ErrorCode.AuthStaffNotFound);
            }

            if (staff.Role == AppRoles.Admin)
            {
                var hasOtherActiveAdmin = await unitOfWork.Staff.ExistsAsync(
                    s => s.Id != staff.Id && s.Role == AppRoles.Admin && s.Status == EntityStatus.Active,
                    ct);
                if (!hasOtherActiveAdmin)
                {
                    return ServiceResult.Failure(
                        ErrorCode.CommonRecordInUse,
                        "Không thể xóa mềm Quản trị viên (ADMIN) hoạt động cuối cùng của hệ thống.");
                }
            }

            staff.Status = EntityStatus.Deleted;
            staff.SecurityStamp = Guid.NewGuid().ToString("N");
            return ServiceResult.Success($"Đã xóa mềm tài khoản cán bộ \"{staff.FullName}\" (chuyển sang DELETED).");
        }, cancellationToken: ct);
    }

    public Task<ServiceResult> RestoreStaffAsync(Guid id, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var staff = await unitOfWork.Staff.FirstOrDefaultAsync(
                s => s.Id == id && s.Status == EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (staff == null)
            {
                return ServiceResult.Failure(ErrorCode.AuthStaffNotFound);
            }

            staff.Status = EntityStatus.Active;
            staff.FailedLoginAttempts = 0;
            staff.LockedUntil = null;
            staff.SecurityStamp = Guid.NewGuid().ToString("N");
            return ServiceResult.Success($"Đã khôi phục tài khoản cán bộ \"{staff.FullName}\" về trạng thái hoạt động.");
        }, cancellationToken: ct);

    public Task<ServiceResult> HardDeleteStaffAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        if (userContext.CurrentUserId == id)
        {
            return Task.FromResult(ServiceResult.Failure(ErrorCode.StaffCannotModifySelf));
        }

        if (!LibraryConstants.IsValidHardDeleteConfirm(confirmCode))
        {
            return Task.FromResult(ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid));
        }

        return unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var staff = await unitOfWork.Staff.FirstOrDefaultAsync(s => s.Id == id, asNoTracking: false, ct);
            if (staff == null)
            {
                return ServiceResult.Failure(ErrorCode.AuthStaffNotFound);
            }

            if (staff.Status != EntityStatus.Deleted)
            {
                return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
            }

            var hasRelatedRecords =
                await unitOfWork.BorrowSlips.ExistsAsync(bs => bs.IssuedBy == id, ct) ||
                await unitOfWork.BorrowDetails.ExistsAsync(bd => bd.ReceivedBy == id, ct) ||
                await unitOfWork.Fines.ExistsAsync(f => f.HandledBy == id, ct);

            if (hasRelatedRecords)
            {
                return ServiceResult.Failure(
                    ErrorCode.CommonRecordInUse,
                    "Không thể xóa vĩnh viễn cán bộ đã phát sinh lịch sử lập phiếu mượn, nhận trả sách hoặc xử lý tiền phạt.");
            }

            var fullName = staff.FullName;
            await unitOfWork.ClearStaffAuditReferencesAsync(id, ct);
            unitOfWork.Staff.Delete(staff);
            return ServiceResult.Success($"Đã xóa vĩnh viễn tài khoản cán bộ \"{fullName}\" khỏi cơ sở dữ liệu.");
        }, cancellationToken: ct);
    }
}
