using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Extensions;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Catalog;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Repositories;
using web_mvc_management.Util;

namespace web_mvc_management.Services;

public class SystemAdminService(
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ISystemAdminService
{
    // =========================================================================
    // 1. QUẢN LÝ TÀI KHOẢN CÁN BỘ & PHÂN QUYỀN (/System/Staff)
    // =========================================================================
    public async Task<PagedResult<StaffListItemViewModel>> GetStaffPagedAsync(StaffFilter filter, CancellationToken ct = default)
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

    public async Task<ServiceResult> CreateStaffAsync(StaffCreateViewModel model, CancellationToken ct = default)
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
        await unitOfWork.SaveChangesAsync(ct);

        var roleLabel = role == AppRoles.Admin ? "Quản trị viên" : "Thủ thư";
        return ServiceResult.Success($"Đã tạo tài khoản cán bộ \"{staff.FullName}\" ({roleLabel}).");
    }

    public async Task<ServiceResult> UpdateStaffAsync(StaffEditViewModel model, CancellationToken ct = default)
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

        // Bảo vệ không cho Admin tự khóa hoặc tự hạ quyền chính mình
        if (userContext.CurrentUserId.HasValue && userContext.CurrentUserId.Value == staff.Id)
        {
            if (targetRole != staff.Role || model.Status != EntityStatus.Active)
            {
                return ServiceResult.Failure(ErrorCode.StaffCannotModifySelf);
            }
        }

        var securitySensitiveChanged = staff.Role != targetRole ||
                                       staff.Status != model.Status ||
                                       !string.IsNullOrWhiteSpace(model.NewPassword);

        staff.FullName = model.FullName.Trim();
        staff.Email = emailTrim;
        staff.Phone = model.Phone?.Trim();
        staff.Role = targetRole;
        staff.Status = model.Status;

        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            staff.PasswordHash = PasswordHasher.HashPassword(model.NewPassword);
        }

        if (securitySensitiveChanged)
        {
            staff.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        unitOfWork.Staff.Update(staff);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã cập nhật tài khoản cán bộ \"{staff.FullName}\".");
    }

    public async Task<ServiceResult> UnlockStaffAccountAsync(Guid id, CancellationToken ct = default)
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
        unitOfWork.Staff.Update(staff);
        await unitOfWork.SaveChangesAsync(ct);

        return ServiceResult.Success($"Đã mở khóa tài khoản cán bộ \"{staff.FullName}\".");
    }

    public async Task<ServiceResult> SoftDeleteStaffAsync(Guid id, CancellationToken ct = default)
    {
        if (userContext.CurrentUserId.HasValue && userContext.CurrentUserId.Value == id)
        {
            return ServiceResult.Failure(ErrorCode.StaffCannotModifySelf);
        }

        var staff = await unitOfWork.Staff.FirstOrDefaultAsync(
            s => s.Id == id && s.Status != EntityStatus.Deleted,
            asNoTracking: false,
            ct);

        if (staff == null)
        {
            return ServiceResult.Failure(ErrorCode.AuthStaffNotFound);
        }

        staff.Status = EntityStatus.Deleted;
        staff.SecurityStamp = Guid.NewGuid().ToString("N");
        unitOfWork.Staff.Update(staff);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã xóa mềm tài khoản cán bộ \"{staff.FullName}\" (chuyển sang DELETED).");
    }

    public async Task<ServiceResult> RestoreStaffAsync(Guid id, CancellationToken ct = default)
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
        unitOfWork.Staff.Update(staff);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã khôi phục tài khoản cán bộ \"{staff.FullName}\" về trạng thái hoạt động.");
    }

    public async Task<ServiceResult> HardDeleteStaffAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        if (userContext.CurrentUserId.HasValue && userContext.CurrentUserId.Value == id)
        {
            return ServiceResult.Failure(ErrorCode.StaffCannotModifySelf);
        }

        var staff = await unitOfWork.Staff.GetByIdAsync(id, ct);
        if (staff == null)
        {
            return ServiceResult.Failure(ErrorCode.AuthStaffNotFound);
        }

        if (staff.Status != EntityStatus.Deleted)
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
        }

        if (!string.Equals(confirmCode?.Trim(), "XOA", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid);
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

        unitOfWork.Staff.Delete(staff);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã xóa vĩnh viễn tài khoản cán bộ \"{staff.FullName}\" khỏi cơ sở dữ liệu.");
    }

    // =========================================================================
    // 2. QUẢN LÝ TÀI KHOẢN & THẺ ĐỘC GIẢ (/Admin/Readers)
    // =========================================================================
    public async Task<PagedResult<ReaderListItemViewModel>> GetReadersPagedAsync(ReaderFilter filter, CancellationToken ct = default)
    {
        var query = unitOfWork.Readers.GetQueryable(asNoTracking: true);

        if (filter.Status == EntityStatus.Deleted)
        {
            query = query.Where(r => r.Status == EntityStatus.Deleted);
        }
        else
        {
            query = query.Where(r => r.Status != EntityStatus.Deleted);
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                query = query.Where(r => r.Status == filter.Status);
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.ToLower();
            query = query.Where(r =>
                r.FullName.ToLower().Contains(kw) ||
                r.CardNo.ToLower().Contains(kw) ||
                r.Email.ToLower().Contains(kw) ||
                r.Username.ToLower().Contains(kw) ||
                (r.MemberCode != null && r.MemberCode.ToLower().Contains(kw)));
        }

        if (filter.ReaderTypeId.HasValue)
        {
            query = query.Where(r => r.ReaderTypeId == filter.ReaderTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.CardStatus))
        {
            query = query.Where(r => r.CardStatus == filter.CardStatus);
        }

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReaderListItemViewModel
            {
                Id = r.Id,
                CardNo = r.CardNo,
                MemberCode = r.MemberCode,
                Username = r.Username,
                Email = r.Email,
                FullName = r.FullName,
                Phone = r.Phone,
                ReaderTypeId = r.ReaderTypeId,
                ReaderTypeName = r.ReaderType.Name,
                MaxBorrowBooks = r.ReaderType.MaxBorrowBooks,
                LoanDays = r.ReaderType.LoanDays,
                CardIssuedDate = r.CardIssuedDate,
                CardExpiryDate = r.CardExpiryDate,
                CardStatus = r.CardStatus,
                LockReason = r.LockReason,
                Status = r.Status,
                ActiveBorrowCount = r.BorrowSlips
                    .Where(s => s.Status != EntityStatus.Deleted)
                    .SelectMany(s => s.BorrowDetails)
                    .Count(d => d.ReturnedAt == null && d.Status != EntityStatus.Deleted)
            })
            .ToPagedResultAsync(filter, ct);
    }

    public async Task<IReadOnlyList<SelectOptionViewModel>> GetReaderTypesSelectAsync(CancellationToken ct = default)
    {
        return await unitOfWork.ReaderTypes.GetQueryable(asNoTracking: true)
            .Where(rt => rt.Status == EntityStatus.Active)
            .OrderBy(rt => rt.Name)
            .Select(rt => new SelectOptionViewModel(
                rt.Id,
                $"{rt.Name} (Tối đa {rt.MaxBorrowBooks} cuốn / {rt.LoanDays} ngày)"))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult> UpdateReaderAccountAsync(ReaderManageViewModel model, CancellationToken ct = default)
    {
        var reader = await unitOfWork.Readers.FirstOrDefaultAsync(
            r => r.Id == model.Id && r.Status != EntityStatus.Deleted,
            asNoTracking: false,
            ct);

        if (reader == null)
        {
            return ServiceResult.Failure(ErrorCode.ReaderNotFound);
        }

        var typeExists = await unitOfWork.ReaderTypes.ExistsAsync(
            rt => rt.Id == model.ReaderTypeId && rt.Status != EntityStatus.Deleted,
            ct);

        if (!typeExists)
        {
            return ServiceResult.Failure(ErrorCode.ReaderTypeNotFound);
        }

        var wasLockedOrInactive = reader.CardStatus != model.CardStatus || reader.Status != model.Status;

        reader.FullName = model.FullName.Trim();
        reader.Phone = model.Phone?.Trim();
        reader.ReaderTypeId = model.ReaderTypeId;
        reader.CardExpiryDate = model.CardExpiryDate;
        reader.CardStatus = model.CardStatus;
        reader.Status = model.Status;

        if (model.CardStatus == LibraryConstants.CardStatus.Locked)
        {
            reader.LockedAt = dateTimeProvider.UtcNow;
            reader.LockReason = string.IsNullOrWhiteSpace(model.LockReason)
                ? "Khóa thẻ theo quyết định của thủ thư/quản trị viên"
                : model.LockReason.Trim();
        }
        else if (model.CardStatus == LibraryConstants.CardStatus.Active)
        {
            reader.LockedAt = null;
            reader.LockReason = null;
            reader.FailedLoginAttempts = 0;
            reader.LockedUntil = null;
        }

        if (wasLockedOrInactive)
        {
            reader.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        unitOfWork.Readers.Update(reader);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã cập nhật thẻ độc giả {reader.CardNo} – {reader.FullName}.");
    }

    public async Task<ServiceResult> SoftDeleteReaderAsync(Guid id, CancellationToken ct = default)
    {
        var reader = await unitOfWork.Readers.FirstOrDefaultAsync(
            r => r.Id == id && r.Status != EntityStatus.Deleted,
            asNoTracking: false,
            ct);

        if (reader == null)
        {
            return ServiceResult.Failure(ErrorCode.ReaderNotFound);
        }

        var hasActiveBorrow = await unitOfWork.BorrowDetails.ExistsAsync(
            bd => bd.BorrowSlip.ReaderId == id &&
                  bd.ReturnedAt == null &&
                  bd.Status != EntityStatus.Deleted,
            ct);

        if (hasActiveBorrow)
        {
            return ServiceResult.Failure(
                ErrorCode.CommonRecordInUse,
                "Không thể xóa mềm độc giả đang giữ sách mượn chưa trả.");
        }

        reader.Status = EntityStatus.Deleted;
        reader.SecurityStamp = Guid.NewGuid().ToString("N");
        unitOfWork.Readers.Update(reader);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã xóa mềm thẻ độc giả {reader.CardNo} – {reader.FullName} (chuyển sang DELETED).");
    }

    public async Task<ServiceResult> RestoreReaderAsync(Guid id, CancellationToken ct = default)
    {
        var reader = await unitOfWork.Readers.FirstOrDefaultAsync(
            r => r.Id == id && r.Status == EntityStatus.Deleted,
            asNoTracking: false,
            ct);

        if (reader == null)
        {
            return ServiceResult.Failure(ErrorCode.ReaderNotFound);
        }

        reader.Status = EntityStatus.Active;
        reader.SecurityStamp = Guid.NewGuid().ToString("N");
        unitOfWork.Readers.Update(reader);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã khôi phục tài khoản độc giả {reader.CardNo} – {reader.FullName}.");
    }

    public async Task<ServiceResult> HardDeleteReaderAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        var reader = await unitOfWork.Readers.GetByIdAsync(id, ct);
        if (reader == null)
        {
            return ServiceResult.Failure(ErrorCode.ReaderNotFound);
        }

        if (reader.Status != EntityStatus.Deleted)
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
        }

        if (!string.Equals(confirmCode?.Trim(), "XOA", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid);
        }

        var hasHistory =
            await unitOfWork.BorrowSlips.ExistsAsync(bs => bs.ReaderId == id, ct) ||
            await unitOfWork.Reservations.ExistsAsync(res => res.ReaderId == id, ct);

        if (hasHistory)
        {
            return ServiceResult.Failure(
                ErrorCode.CommonRecordInUse,
                "Không thể xóa vĩnh viễn độc giả đã phát sinh lịch sử phiếu mượn hoặc đặt trước sách.");
        }

        unitOfWork.Readers.Delete(reader);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã xóa vĩnh viễn độc giả {reader.CardNo} – {reader.FullName} khỏi cơ sở dữ liệu.");
    }

    // =========================================================================
    // 3. CẤU HÌNH LOẠI ĐỘC GIẢ (/Admin/ReaderTypes)
    // =========================================================================
    public async Task<IReadOnlyList<ReaderTypeItemViewModel>> GetReaderTypesAsync(CancellationToken ct = default)
    {
        return await unitOfWork.ReaderTypes.GetQueryable(asNoTracking: true)
            .OrderBy(rt => rt.Status == EntityStatus.Deleted ? 1 : 0)
            .ThenByDescending(rt => rt.MaxBorrowBooks)
            .Select(rt => new ReaderTypeItemViewModel
            {
                Id = rt.Id,
                Code = rt.Code,
                Name = rt.Name,
                MaxBorrowBooks = rt.MaxBorrowBooks,
                LoanDays = rt.LoanDays,
                MaxRenewals = rt.MaxRenewals,
                RenewDays = rt.RenewDays,
                CardValidMonths = rt.CardValidMonths,
                Description = rt.Description,
                Status = rt.Status,
                ReaderCount = rt.Readers.Count(r => r.Status != EntityStatus.Deleted)
            })
            .ToListAsync(ct);
    }

    public async Task<ServiceResult> SaveReaderTypeAsync(ReaderTypeFormViewModel model, CancellationToken ct = default)
    {
        var codeTrim = model.Code.Trim().ToUpperInvariant();
        var duplicate = await unitOfWork.ReaderTypes.ExistsAsync(
            rt => rt.Code.ToUpper() == codeTrim && rt.Id != model.Id,
            ct);

        if (duplicate)
        {
            return ServiceResult.ValidationError(ErrorCode.ReaderTypeCodeAlreadyExists);
        }

        if (model.Id.HasValue && model.Id.Value != Guid.Empty)
        {
            var entity = await unitOfWork.ReaderTypes.FirstOrDefaultAsync(
                rt => rt.Id == model.Id.Value && rt.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);

            if (entity == null)
            {
                return ServiceResult.Failure(ErrorCode.ReaderTypeNotFound);
            }

            entity.Code = codeTrim;
            entity.Name = model.Name.Trim();
            entity.MaxBorrowBooks = model.MaxBorrowBooks;
            entity.LoanDays = model.LoanDays;
            entity.MaxRenewals = model.MaxRenewals;
            entity.RenewDays = model.RenewDays;
            entity.CardValidMonths = model.CardValidMonths;
            entity.Description = model.Description?.Trim();
            entity.Status = model.Status;

            unitOfWork.ReaderTypes.Update(entity);
            await unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success($"Đã cập nhật quy định loại độc giả \"{entity.Name}\" ({entity.MaxBorrowBooks} cuốn / {entity.LoanDays} ngày).");
        }
        else
        {
            var entity = new ReaderType
            {
                Id = Guid.NewGuid(),
                Code = codeTrim,
                Name = model.Name.Trim(),
                MaxBorrowBooks = model.MaxBorrowBooks,
                LoanDays = model.LoanDays,
                MaxRenewals = model.MaxRenewals,
                RenewDays = model.RenewDays,
                CardValidMonths = model.CardValidMonths,
                Description = model.Description?.Trim(),
                Status = model.Status
            };

            await unitOfWork.ReaderTypes.AddAsync(entity, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success($"Đã thêm loại độc giả \"{entity.Name}\".");
        }
    }

    public async Task<ServiceResult> DeleteReaderTypeAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await unitOfWork.ReaderTypes.FirstOrDefaultAsync(
            rt => rt.Id == id && rt.Status != EntityStatus.Deleted,
            asNoTracking: false,
            ct);

        if (entity == null)
        {
            return ServiceResult.Failure(ErrorCode.ReaderTypeNotFound);
        }

        var hasReaders = await unitOfWork.Readers.ExistsAsync(
            r => r.ReaderTypeId == id && r.Status != EntityStatus.Deleted,
            ct);

        if (hasReaders)
        {
            return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
        }

        entity.Status = EntityStatus.Deleted;
        unitOfWork.ReaderTypes.Update(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã xóa mềm loại độc giả \"{entity.Name}\" (chuyển sang DELETED).");
    }

    public async Task<ServiceResult> RestoreReaderTypeAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await unitOfWork.ReaderTypes.FirstOrDefaultAsync(
            rt => rt.Id == id && rt.Status == EntityStatus.Deleted,
            asNoTracking: false,
            ct);

        if (entity == null)
        {
            return ServiceResult.Failure(ErrorCode.ReaderTypeNotFound);
        }

        entity.Status = EntityStatus.Active;
        unitOfWork.ReaderTypes.Update(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã khôi phục loại độc giả \"{entity.Name}\" về trạng thái hoạt động.");
    }

    public async Task<ServiceResult> HardDeleteReaderTypeAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        var entity = await unitOfWork.ReaderTypes.GetByIdAsync(id, ct);
        if (entity == null)
        {
            return ServiceResult.Failure(ErrorCode.ReaderTypeNotFound);
        }

        if (entity.Status != EntityStatus.Deleted)
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
        }

        if (!string.Equals(confirmCode?.Trim(), "XOA", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid);
        }

        var hasAnyReaders = await unitOfWork.Readers.ExistsAsync(r => r.ReaderTypeId == id, ct);
        if (hasAnyReaders)
        {
            return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
        }

        unitOfWork.ReaderTypes.Delete(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã xóa vĩnh viễn loại độc giả \"{entity.Name}\" khỏi cơ sở dữ liệu.");
    }

    // =========================================================================
    // 4. CẤU HÌNH QUY ĐỊNH MƯỢN TRẢ & MỨC PHẠT (/System/Settings)
    // =========================================================================
    public async Task<LibrarySettingFormViewModel> GetLibrarySettingAsync(CancellationToken ct = default)
    {
        var setting = await unitOfWork.LibrarySettings.GetQueryable(asNoTracking: true)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (setting == null)
        {
            return new LibrarySettingFormViewModel();
        }

        return new LibrarySettingFormViewModel
        {
            Id = setting.Id,
            OverdueFinePerDay = setting.OverdueFinePerDay,
            DamagedFinePercent = setting.DamagedFinePercent,
            LostFinePercent = setting.LostFinePercent,
            MaxUnpaidFine = setting.MaxUnpaidFine,
            ReservationHoldHours = setting.ReservationHoldHours,
            UpdatedAt = setting.UpdatedAt
        };
    }

    public async Task<ServiceResult> SaveLibrarySettingAsync(LibrarySettingFormViewModel model, CancellationToken ct = default)
    {
        var setting = await unitOfWork.LibrarySettings.GetQueryable(asNoTracking: false)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (setting == null)
        {
            setting = new LibrarySetting
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                OverdueFinePerDay = model.OverdueFinePerDay,
                DamagedFinePercent = model.DamagedFinePercent,
                LostFinePercent = model.LostFinePercent,
                MaxUnpaidFine = model.MaxUnpaidFine,
                ReservationHoldHours = model.ReservationHoldHours,
                Status = EntityStatus.Active
            };
            await unitOfWork.LibrarySettings.AddAsync(setting, ct);
        }
        else
        {
            setting.OverdueFinePerDay = model.OverdueFinePerDay;
            setting.DamagedFinePercent = model.DamagedFinePercent;
            setting.LostFinePercent = model.LostFinePercent;
            setting.MaxUnpaidFine = model.MaxUnpaidFine;
            setting.ReservationHoldHours = model.ReservationHoldHours;
            setting.Status = EntityStatus.Active;
            unitOfWork.LibrarySettings.Update(setting);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã lưu cấu hình quy định mức phạt và vận hành thư viện.");
    }
}

