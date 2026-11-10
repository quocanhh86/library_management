using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Extensions;
using web_mvc_management.Models.ViewModels.Catalog;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Repositories;

namespace web_mvc_management.Services;

public class ReaderAdminService(IUnitOfWork unitOfWork) : IReaderAdminService
{
    // =========================================================================
    // DANH SÁCH + LỌC + PHÂN TRANG
    // =========================================================================
    public async Task<AdminReadersPageViewModel> GetPageAsync(ReaderFilter filter, CancellationToken ct = default)
    {
        var query = unitOfWork.Readers.GetQueryable(asNoTracking: true);

        // Mặc định ẩn bản ghi đã xóa mềm; chỉ hiện khi chọn Status = DELETED
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
                r.CardNo.ToLower().Contains(kw) ||
                r.FullName.ToLower().Contains(kw) ||
                r.Email.ToLower().Contains(kw) ||
                r.Username.ToLower().Contains(kw) ||
                (r.MemberCode != null && r.MemberCode.ToLower().Contains(kw)) ||
                (r.Phone != null && r.Phone.Contains(kw)));
        }

        if (filter.ReaderTypeId.HasValue)
        {
            query = query.Where(r => r.ReaderTypeId == filter.ReaderTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.CardStatus))
        {
            query = query.Where(r => r.CardStatus == filter.CardStatus);
        }

        var paged = await query
            .OrderBy(r => r.FullName)
            .ThenBy(r => r.CardNo)
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
                    .Where(bs => bs.Status != EntityStatus.Deleted)
                    .SelectMany(bs => bs.BorrowDetails)
                    .Count(bd => bd.ReturnedAt == null && bd.Status != EntityStatus.Deleted)
            })
            .ToPagedResultAsync(filter, ct);

        var readerTypes = await unitOfWork.ReaderTypes.GetQueryable(asNoTracking: true)
            .Where(rt => rt.Status != EntityStatus.Deleted)
            .OrderBy(rt => rt.Name)
            .Select(rt => new SelectOptionViewModel(rt.Id, rt.Name))
            .ToListAsync(ct);

        return new AdminReadersPageViewModel
        {
            PagedReaders = paged,
            Filter = filter,
            ReaderTypes = readerTypes
        };
    }

    // =========================================================================
    // CẬP NHẬT THẺ / TÀI KHOẢN
    // =========================================================================
    public async Task<ServiceResult> UpdateAsync(ReaderManageViewModel model, CancellationToken ct = default)
    {
        string[] validCardStatuses =
        [
            LibraryConstants.CardStatus.Active,
            LibraryConstants.CardStatus.Locked,
            LibraryConstants.CardStatus.Expired
        ];

        if (!validCardStatuses.Contains(model.CardStatus))
        {
            return ServiceResult.ValidationError("Trạng thái thẻ không hợp lệ.");
        }

        // Form chỉ cho chọn ACTIVE / INACTIVE; DELETED đi qua nút "Xóa mềm"
        if (model.Status != EntityStatus.Active && model.Status != EntityStatus.Inactive)
        {
            return ServiceResult.ValidationError("Trạng thái tài khoản không hợp lệ.");
        }

        if (model.CardStatus == LibraryConstants.CardStatus.Locked &&
            string.IsNullOrWhiteSpace(model.LockReason))
        {
            return ServiceResult.ValidationError("Vui lòng nhập lý do khi khóa thẻ.");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        if (model.CardStatus == LibraryConstants.CardStatus.Active && model.CardExpiryDate < today)
        {
            return ServiceResult.ValidationError("Thẻ ACTIVE phải có ngày hết hạn từ hôm nay trở đi. Hãy gia hạn ngày hết hạn hoặc chọn EXPIRED.");
        }

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var reader = await unitOfWork.Readers.FirstOrDefaultAsync(
                r => r.Id == model.Id && r.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);

            if (reader == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReaderNotFound);
            }

            var readerTypeOk = await unitOfWork.ReaderTypes.ExistsAsync(
                rt => rt.Id == model.ReaderTypeId && rt.Status != EntityStatus.Deleted,
                ct);

            if (!readerTypeOk)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReaderTypeNotFound);
            }

            var securitySensitiveChanged =
                reader.Status != model.Status ||
                reader.CardStatus != model.CardStatus;

            reader.FullName = model.FullName.Trim();
            reader.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
            reader.ReaderTypeId = model.ReaderTypeId;
            reader.CardExpiryDate = model.CardExpiryDate;
            reader.Status = model.Status;

            if (model.CardStatus == LibraryConstants.CardStatus.Locked)
            {
                reader.LockedAt ??= DateTimeOffset.UtcNow;
                reader.LockReason = model.LockReason!.Trim();
            }
            else
            {
                reader.LockedAt = null;
                reader.LockReason = null;
            }

            reader.CardStatus = model.CardStatus;

            if (securitySensitiveChanged)
            {
                // Đổi stamp để thu hồi phiên đăng nhập đang mở
                reader.SecurityStamp = Guid.NewGuid().ToString("N");
            }

            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã cập nhật thẻ độc giả \"{reader.CardNo} - {reader.FullName}\".");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // =========================================================================
    // XÓA MỀM -> KHÔI PHỤC -> XÓA VĨNH VIỄN (3 bước như các màn hình khác)
    // =========================================================================
    public async Task<ServiceResult> SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var reader = await unitOfWork.Readers.FirstOrDefaultAsync(
                r => r.Id == id && r.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);

            if (reader == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReaderNotFound);
            }

            var stillHoldingBooks = await unitOfWork.BorrowDetails.ExistsAsync(
                bd => bd.BorrowSlip.ReaderId == id && bd.ReturnedAt == null && bd.Status != EntityStatus.Deleted,
                ct);

            if (stillHoldingBooks)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(
                    ErrorCode.CommonRecordInUse,
                    "Không thể xóa mềm độc giả đang giữ sách chưa trả. Nhận trả sách trước.");
            }

            var hasUnpaidFines = await unitOfWork.Fines.ExistsAsync(
                f => f.BorrowDetail.BorrowSlip.ReaderId == id &&
                     f.FineStatus == LibraryConstants.FineStatus.Unpaid &&
                     f.Status != EntityStatus.Deleted,
                ct);

            if (hasUnpaidFines)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(
                    ErrorCode.CommonRecordInUse,
                    "Không thể xóa mềm độc giả đang còn nợ tiền phạt chưa thanh toán. Vui lòng thu hoặc miễn phạt trước.");
            }

            // Tự động đóng các phiếu đặt trước đang mở (PENDING / READY) và giải phóng bản sao đang giữ (nếu có)
            var activeReservations = await unitOfWork.Reservations.GetQueryable(asNoTracking: false)
                .Include(r => r.AssignedCopy)
                .Where(r => r.ReaderId == id &&
                            r.Status != EntityStatus.Deleted &&
                            (r.ReservationStatus == LibraryConstants.ReservationStatus.Pending ||
                             r.ReservationStatus == LibraryConstants.ReservationStatus.Ready))
                .ToListAsync(ct);

            if (activeReservations.Count > 0)
            {
                var now = DateTimeOffset.UtcNow;
                var setting = await unitOfWork.LibrarySettings.GetQueryable(asNoTracking: true)
                    .OrderByDescending(s => s.UpdatedAt)
                    .FirstOrDefaultAsync(ct);
                var holdHours = setting?.ReservationHoldHours ?? (short)48;

                foreach (var res in activeReservations)
                {
                    var wasReady = res.ReservationStatus == LibraryConstants.ReservationStatus.Ready;
                    res.ReservationStatus = LibraryConstants.ReservationStatus.Cancelled;
                    res.ClosedAt = now;

                    if (wasReady && res.AssignedCopy is { CopyStatus: LibraryConstants.CopyStatus.Reserved } heldCopy)
                    {
                        var nextPending = await unitOfWork.Reservations.GetQueryable(asNoTracking: false)
                            .Where(r => r.Status != EntityStatus.Deleted &&
                                        r.BookId == heldCopy.BookId &&
                                        r.ReaderId != id &&
                                        r.Reader.Status == EntityStatus.Active &&
                                        r.ReservationStatus == LibraryConstants.ReservationStatus.Pending)
                            .OrderBy(r => r.ReservedAt)
                            .FirstOrDefaultAsync(ct);

                        if (nextPending == null)
                        {
                            heldCopy.CopyStatus = LibraryConstants.CopyStatus.Available;
                        }
                        else
                        {
                            nextPending.ReservationStatus = LibraryConstants.ReservationStatus.Ready;
                            nextPending.AssignedCopyId = heldCopy.Id;
                            nextPending.ReadyAt = now;
                            nextPending.ExpiresAt = now.AddHours(holdHours);
                            heldCopy.CopyStatus = LibraryConstants.CopyStatus.Reserved;
                        }
                    }
                }
            }

            reader.Status = EntityStatus.Deleted;
            reader.SecurityStamp = Guid.NewGuid().ToString("N");
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã xóa mềm độc giả \"{reader.CardNo} - {reader.FullName}\" (chuyển sang DELETED).");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> RestoreAsync(Guid id, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var reader = await unitOfWork.Readers.FirstOrDefaultAsync(
                r => r.Id == id && r.Status == EntityStatus.Deleted,
                asNoTracking: false,
                ct);

            if (reader == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReaderNotFound);
            }

            reader.Status = EntityStatus.Active;
            reader.FailedLoginAttempts = 0;
            reader.LockedUntil = null;
            reader.SecurityStamp = Guid.NewGuid().ToString("N");
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã khôi phục độc giả \"{reader.CardNo} - {reader.FullName}\".");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> HardDeleteAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        if (!string.Equals(confirmCode?.Trim(), "XOA", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid);
        }

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var reader = await unitOfWork.Readers.FirstOrDefaultAsync(r => r.Id == id, asNoTracking: false, ct);
            if (reader == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReaderNotFound);
            }

            if (reader.Status != EntityStatus.Deleted)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
            }

            var hasHistory =
                await unitOfWork.BorrowSlips.ExistsAsync(bs => bs.ReaderId == id, ct) ||
                await unitOfWork.Reservations.ExistsAsync(rv => rv.ReaderId == id, ct);

            if (hasHistory)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(
                    ErrorCode.CommonRecordInUse,
                    "Không thể xóa vĩnh viễn độc giả đã phát sinh phiếu mượn hoặc đặt trước sách.");
            }

            var label = $"{reader.CardNo} - {reader.FullName}";
            unitOfWork.Readers.Delete(reader);
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã xóa vĩnh viễn độc giả \"{label}\" khỏi cơ sở dữ liệu.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }
}
