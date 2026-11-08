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

public class SystemAdminService(
    IUnitOfWork unitOfWork,
    IUserContext userContext) : ISystemAdminService
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
    // 2. CẤU HÌNH LOẠI ĐỘC GIẢ (/Admin/ReaderTypes)
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
    // 3. CẤU HÌNH QUY ĐỊNH MƯỢN TRẢ & MỨC PHẠT (/System/Settings)
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


 public async Task<DashboardViewModel> GetDashboardAsync(
    int year,
    CancellationToken ct)
{
    var readersQuery = unitOfWork.Readers
        .GetQueryable(asNoTracking: true);

    var booksQuery = unitOfWork.Books
        .GetQueryable(asNoTracking: true);

    var copiesQuery = unitOfWork.BookCopies
        .GetQueryable(asNoTracking: true);

    var slipsQuery = unitOfWork.BorrowSlips
        .GetQueryable(asNoTracking: true);

    var detailsQuery = unitOfWork.BorrowDetails
        .GetQueryable(asNoTracking: true);

    var finesQuery = unitOfWork.Fines
        .GetQueryable(asNoTracking: true);

    // =========================
    // 1. Tổng quan
    // =========================

    var totalReaders = await readersQuery.CountAsync(ct);

    var totalBooks = await booksQuery.CountAsync(ct);

    var totalBookCopies = await copiesQuery.CountAsync(ct);

    var borrowingBooks = await detailsQuery
        .CountAsync(x => x.ReturnedAt == null, ct);

    var totalBorrowSlips = await slipsQuery.CountAsync(ct);

    var totalFines = await finesQuery
        .SumAsync(x => x.Amount, ct);

    // =========================
    // 2. Thống kê mượn theo tháng
    // =========================

    var monthlyData = await slipsQuery
        .Where(x => x.BorrowedAt.Year == year)
        .GroupBy(x => x.BorrowedAt.Month)
        .Select(g => new
        {
            Month = g.Key,
            BorrowCount = g.Count()
        })
        .ToListAsync(ct);

    var monthlyStatistics = Enumerable.Range(1, 12)
        .Select(month => new DashboardMonthlyItemViewModel
        {
            MonthNumber = month,
            Month = $"Tháng {month}",
            BorrowCount = monthlyData
                .FirstOrDefault(x => x.Month == month)?.BorrowCount ?? 0
        })
        .ToList();
    // 3. Sách được mượn nhiều nhất
    // =========================

    var mostBorrowedBooks = await detailsQuery
        .Where(x => x.BorrowSlip.BorrowedAt.Year == year)
        .GroupBy(x => new
        {
            x.BookCopy.BookId,
            x.BookCopy.Book.Title
        })
        .Select(g => new DashboardBookRankingViewModel
        {
            BookId = g.Key.BookId,
            BookTitle = g.Key.Title,
            BorrowCount = g.Count()
        })
        .OrderByDescending(x => x.BorrowCount)
        .Take(5)
        .ToListAsync(ct);

    // =========================
    // 4. Sách ít được mượn
    // =========================

    var leastBorrowedBooks = await booksQuery
        .Select(x => new DashboardBookRankingViewModel
        {
            BookId = x.Id,
            BookTitle = x.Title,

            BorrowCount = x.BookCopies
                .SelectMany(c => c.BorrowDetails)
                .Count(d => d.BorrowSlip.BorrowedAt.Year == year)
        })
        .OrderBy(x => x.BorrowCount)
        .ThenBy(x => x.BookTitle)
        .Take(5)
        .ToListAsync(ct);

    // =========================
    // 5. Thể loại được mượn nhiều nhất
    // =========================

    var mostBorrowedCategories = await detailsQuery
        .Where(x =>
            x.BorrowSlip.BorrowedAt.Year == year &&
            x.BookCopy.Book.CategoryId != null)
        .GroupBy(x => new
        {
            x.BookCopy.Book.CategoryId,
            x.BookCopy.Book.Category!.Name
        })
        .Select(g => new DashboardCategoryRankingViewModel
        {
            CategoryId = g.Key.CategoryId,
            CategoryName = g.Key.Name,
            BorrowCount = g.Count()
        })
        .OrderByDescending(x => x.BorrowCount)
        .Take(5)
        .ToListAsync(ct);

    // =========================
    // 6. Độc giả mượn nhiều nhất
    // =========================

    var mostActiveReaders = await detailsQuery
        .Where(x => x.BorrowSlip.BorrowedAt.Year == year)
        .GroupBy(x => new
        {
            x.BorrowSlip.ReaderId,
            x.BorrowSlip.Reader.FullName
        })
        .Select(g => new DashboardReaderRankingViewModel
        {
            ReaderId = g.Key.ReaderId,
            ReaderName = g.Key.FullName,
            BorrowCount = g.Count()
        })
        .OrderByDescending(x => x.BorrowCount)
        .Take(5)
        .ToListAsync(ct);

    // return new DashboardViewModel
    // {
    //     TotalReaders = totalReaders,
    //     TotalBooks = totalBooks,
    //     TotalBookCopies = totalBookCopies,
    //     BorrowingBooks = borrowingBooks,
    //     TotalBorrowSlips = totalBorrowSlips,
    //     TotalFines = totalFines,

    //     MonthlyStatistics = monthlyStatistics,
    //     MostBorrowedBooks = mostBorrowedBooks,
    //     LeastBorrowedBooks = leastBorrowedBooks,
    //     MostBorrowedCategories = mostBorrowedCategories,
    //     MostActiveReaders = mostActiveReaders
    // };
    return new DashboardViewModel
{
    TotalReaders = totalReaders,
    TotalBooks = totalBooks,
    TotalBookCopies = totalBookCopies,
    BorrowingBooks = borrowingBooks,
    TotalBorrowSlips = totalBorrowSlips,
    TotalFines = totalFines,

    SelectedYear = year,

    MonthlyStatistics = monthlyStatistics,
    MostBorrowedBooks = mostBorrowedBooks,
    LeastBorrowedBooks = leastBorrowedBooks,
    MostBorrowedCategories = mostBorrowedCategories,
    MostActiveReaders = mostActiveReaders
};
}



}

