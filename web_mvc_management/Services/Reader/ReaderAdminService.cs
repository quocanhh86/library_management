using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Extensions;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Catalog;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Repositories;

namespace web_mvc_management.Services;

public class ReaderAdminService(
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    IReservationQueueEngine reservationQueueEngine) : IReaderAdminService
{
    public async Task<AdminReadersPageViewModel> GetPageAsync(ReaderFilter filter, CancellationToken ct = default)
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

        var today = dateTimeProvider.Today;

        if (!string.IsNullOrWhiteSpace(filter.CardStatus))
        {
            if (filter.CardStatus == LibraryConstants.CardStatus.Expired)
            {
                query = query.Where(r =>
                    r.CardStatus == LibraryConstants.CardStatus.Expired ||
                    (r.CardStatus == LibraryConstants.CardStatus.Active && r.CardExpiryDate < today));
            }
            else if (filter.CardStatus == LibraryConstants.CardStatus.Active)
            {
                query = query.Where(r => r.CardStatus == LibraryConstants.CardStatus.Active && r.CardExpiryDate >= today);
            }
            else
            {
                query = query.Where(r => r.CardStatus == filter.CardStatus);
            }
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
                CardStatus = r.CardStatus == LibraryConstants.CardStatus.Locked
                    ? LibraryConstants.CardStatus.Locked
                    : (r.CardExpiryDate < today ? LibraryConstants.CardStatus.Expired : r.CardStatus),
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

    public Task<ServiceResult> UpdateAsync(ReaderManageViewModel model, CancellationToken ct = default)
    {
        if (model.CardStatus is not (LibraryConstants.CardStatus.Active or LibraryConstants.CardStatus.Locked or LibraryConstants.CardStatus.Expired))
        {
            return Task.FromResult(ServiceResult.ValidationError("Trạng thái thẻ không hợp lệ."));
        }

        if (model.Status != EntityStatus.Active && model.Status != EntityStatus.Inactive)
        {
            return Task.FromResult(ServiceResult.ValidationError("Trạng thái tài khoản không hợp lệ."));
        }

        if (model.CardStatus == LibraryConstants.CardStatus.Locked && string.IsNullOrWhiteSpace(model.LockReason))
        {
            return Task.FromResult(ServiceResult.ValidationError("Vui lòng nhập lý do khi khóa thẻ."));
        }

        var today = dateTimeProvider.Today;
        if (model.CardStatus == LibraryConstants.CardStatus.Active && model.CardExpiryDate < today)
        {
            return Task.FromResult(ServiceResult.ValidationError(
                "Thẻ ACTIVE phải có ngày hết hạn từ hôm nay trở đi. Hãy gia hạn ngày hết hạn hoặc chọn EXPIRED."));
        }

        return unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var reader = await unitOfWork.Readers.FirstOrDefaultAsync(
                r => r.Id == model.Id && r.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);

            if (reader == null)
            {
                return ServiceResult.Failure(ErrorCode.ReaderNotFound);
            }

            if (model.CardExpiryDate <= reader.CardIssuedDate)
            {
                return ServiceResult.ValidationError(
                    $"Ngày hết hạn thẻ phải sau ngày cấp thẻ ({reader.CardIssuedDate:dd/MM/yyyy}).");
            }

            var phoneTrim = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
            if (phoneTrim != null &&
                await unitOfWork.Readers.ExistsAsync(r => r.Id != reader.Id && r.Phone == phoneTrim, ct))
            {
                return ServiceResult.ValidationError(ErrorCode.RegisterDuplicatePhone);
            }

            if (!await unitOfWork.ReaderTypes.ExistsAsync(rt => rt.Id == model.ReaderTypeId && rt.Status != EntityStatus.Deleted, ct))
            {
                return ServiceResult.Failure(ErrorCode.ReaderTypeNotFound);
            }

            var securitySensitiveChanged = reader.Status != model.Status || reader.CardStatus != model.CardStatus;

            reader.FullName = model.FullName.Trim();
            reader.Phone = phoneTrim;
            reader.ReaderTypeId = model.ReaderTypeId;
            reader.CardExpiryDate = model.CardExpiryDate;
            reader.Status = model.Status;

            if (model.CardStatus == LibraryConstants.CardStatus.Locked)
            {
                reader.LockedAt ??= dateTimeProvider.UtcNow;
                reader.LockReason = model.LockReason!.Trim();
            }
            else
            {
                reader.LockedAt = null;
                reader.LockReason = null;
            }

            reader.CardStatus = model.CardStatus;

            // Nếu thẻ bị khóa/hết hạn hoặc tài khoản bị vô hiệu hóa, giải phóng các phiếu đặt trước đang mở tránh kẹt sách (Copy Trap)
            if (model.CardStatus != LibraryConstants.CardStatus.Active ||
                model.CardExpiryDate < today ||
                model.Status != EntityStatus.Active)
            {
                await reservationQueueEngine.CancelReaderOpenReservationsAsync(reader.Id, ct);
            }

            if (securitySensitiveChanged)
            {
                reader.SecurityStamp = Guid.NewGuid().ToString("N");
            }

            return ServiceResult.Success($"Đã cập nhật thẻ độc giả \"{reader.CardNo} - {reader.FullName}\".");
        }, cancellationToken: ct);
    }

    public Task<ServiceResult> SoftDeleteAsync(Guid id, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var reader = await unitOfWork.Readers.FirstOrDefaultAsync(
                r => r.Id == id && r.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (reader == null)
            {
                return ServiceResult.Failure(ErrorCode.ReaderNotFound);
            }

            var stillHoldingBooks = await unitOfWork.BorrowDetails.ExistsAsync(
                bd => bd.BorrowSlip.ReaderId == id && bd.ReturnedAt == null && bd.Status != EntityStatus.Deleted,
                ct);
            if (stillHoldingBooks)
            {
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
                return ServiceResult.Failure(
                    ErrorCode.CommonRecordInUse,
                    "Không thể xóa mềm độc giả đang còn nợ tiền phạt chưa thanh toán. Vui lòng thu hoặc miễn phạt trước.");
            }

            await reservationQueueEngine.CancelReaderOpenReservationsAsync(id, ct);

            reader.Status = EntityStatus.Deleted;
            reader.CardStatus = LibraryConstants.CardStatus.Locked;
            reader.LockedAt ??= dateTimeProvider.UtcNow;
            reader.LockReason ??= "Đã xóa mềm hồ sơ độc giả";
            reader.SecurityStamp = Guid.NewGuid().ToString("N");

            return ServiceResult.Success($"Đã xóa mềm độc giả \"{reader.CardNo} - {reader.FullName}\" (chuyển sang DELETED).");
        }, cancellationToken: ct);

    public Task<ServiceResult> RestoreAsync(Guid id, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
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
            if (reader.CardStatus == LibraryConstants.CardStatus.Locked &&
                string.Equals(reader.LockReason, "Đã xóa mềm hồ sơ độc giả", StringComparison.Ordinal))
            {
                reader.CardStatus = LibraryConstants.CardStatus.Active;
                reader.LockedAt = null;
                reader.LockReason = null;
            }
            reader.FailedLoginAttempts = 0;
            reader.LockedUntil = null;
            reader.SecurityStamp = Guid.NewGuid().ToString("N");

            return ServiceResult.Success($"Đã khôi phục độc giả \"{reader.CardNo} - {reader.FullName}\".");
        }, cancellationToken: ct);

    public Task<ServiceResult> HardDeleteAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        if (!LibraryConstants.IsValidHardDeleteConfirm(confirmCode))
        {
            return Task.FromResult(ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid));
        }

        return unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var reader = await unitOfWork.Readers.FirstOrDefaultAsync(r => r.Id == id, asNoTracking: false, ct);
            if (reader == null)
            {
                return ServiceResult.Failure(ErrorCode.ReaderNotFound);
            }

            if (reader.Status != EntityStatus.Deleted)
            {
                return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
            }

            var hasHistory = await unitOfWork.BorrowSlips.ExistsAsync(bs => bs.ReaderId == id, ct) ||
                             await unitOfWork.Reservations.ExistsAsync(rv => rv.ReaderId == id, ct);
            if (hasHistory)
            {
                return ServiceResult.Failure(
                    ErrorCode.CommonRecordInUse,
                    "Không thể xóa vĩnh viễn độc giả đã phát sinh phiếu mượn hoặc đặt trước sách.");
            }

            var label = $"{reader.CardNo} - {reader.FullName}";
            unitOfWork.Readers.Delete(reader);
            return ServiceResult.Success($"Đã xóa vĩnh viễn độc giả \"{label}\" khỏi cơ sở dữ liệu.");
        }, cancellationToken: ct);
    }
}
