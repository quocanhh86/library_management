using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Extensions;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.AdminCatalog;
using web_mvc_management.Repositories;

namespace web_mvc_management.Services;

public class BookCopyService(
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    IReservationQueueEngine reservationQueueEngine) : IBookCopyService
{
    public async Task<PagedResult<AdminBookCopyListItemViewModel>> GetBookCopiesPagedAsync(
        AdminBookCopyFilter filter,
        CancellationToken ct = default)
    {
        var query = unitOfWork.BookCopies.GetQueryable(asNoTracking: true);

        if (filter.Status == EntityStatus.Deleted)
        {
            query = query.Where(c => c.Status == EntityStatus.Deleted);
        }
        else
        {
            query = query.Where(c => c.Status != EntityStatus.Deleted && c.Book.Status != EntityStatus.Deleted);
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                query = query.Where(c => c.Status == filter.Status);
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.ToLower();
            query = query.Where(c =>
                c.Barcode.ToLower().Contains(kw) ||
                c.Book.Title.ToLower().Contains(kw) ||
                (c.Book.Isbn != null && c.Book.Isbn.ToLower().Contains(kw)));
        }

        if (filter.BookId.HasValue)
        {
            query = query.Where(c => c.BookId == filter.BookId.Value);
        }

        if (filter.ShelfId.HasValue)
        {
            query = query.Where(c => c.ShelfId == filter.ShelfId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.CopyStatus))
        {
            query = query.Where(c => c.CopyStatus == filter.CopyStatus);
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Barcode)
            .Select(c => new AdminBookCopyListItemViewModel
            {
                Id = c.Id,
                BookId = c.BookId,
                BookTitle = c.Book.Title,
                Isbn = c.Book.Isbn,
                Barcode = c.Barcode,
                ShelfId = c.ShelfId,
                ShelfCode = c.Shelf != null ? c.Shelf.Code : "Chưa xếp kệ",
                CopyStatus = c.CopyStatus,
                Status = c.Status,
                Price = c.Price,
                AcquiredDate = c.AcquiredDate,
                DisposedAt = c.DisposedAt,
                DisposalReason = c.DisposalReason,
                Note = c.Note
            })
            .ToPagedResultAsync(filter, ct);
    }

    public Task<ServiceResult<int>> CreateBookCopiesBatchAsync(BookCopyBatchCreateViewModel model, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var book = await unitOfWork.Books.FirstOrDefaultAsync(
                b => b.Id == model.BookId && b.Status != EntityStatus.Deleted,
                asNoTracking: true,
                ct);
            if (book == null)
            {
                return ServiceResult<int>.Failure(ErrorCode.BookNotFound);
            }

            if (!await unitOfWork.Shelves.ExistsAsync(s => s.Id == model.ShelfId && s.Status != EntityStatus.Deleted, ct))
            {
                return ServiceResult<int>.Failure(ErrorCode.ShelfNotFound);
            }

            var utcNow = dateTimeProvider.UtcNow;
            var acquiredDate = model.AcquiredDate ?? dateTimeProvider.Today;

            if (model.Quantity == 1 && !string.IsNullOrWhiteSpace(model.CustomBarcode))
            {
                var customTrim = model.CustomBarcode.Trim().ToUpperInvariant();
                if (await unitOfWork.BookCopies.ExistsAsync(c => c.Barcode.ToUpper() == customTrim, ct))
                {
                    return ServiceResult<int>.ValidationError(ErrorCode.BookCopyBarcodeAlreadyExists);
                }

                var singleCopy = BuildCopy(book.Id, model.ShelfId, customTrim, model.Price, acquiredDate, model.Note);
                await unitOfWork.BookCopies.AddAsync(singleCopy, ct);

                if (book.Status == EntityStatus.Active)
                {
                    await reservationQueueEngine.AssignCopyToNextPendingOrReleaseAsync(singleCopy, ct: ct);
                }

                return ServiceResult<int>.Success(1, $"Đã nhập bản sao mã vạch {customTrim} cho sách \"{book.Title}\".");
            }

            var generatedBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < model.Quantity; i++)
            {
                var barcode = await BookCatalogMapper.GenerateUniqueBarcodeAsync(unitOfWork, utcNow, generatedBarcodes, ct);
                generatedBarcodes.Add(barcode);

                var batchCopy = BuildCopy(book.Id, model.ShelfId, barcode, model.Price, acquiredDate, model.Note);
                await unitOfWork.BookCopies.AddAsync(batchCopy, ct);

                if (book.Status == EntityStatus.Active)
                {
                    await reservationQueueEngine.AssignCopyToNextPendingOrReleaseAsync(batchCopy, ct: ct);
                }
            }

            return ServiceResult<int>.Success(model.Quantity, $"Đã nhập bổ sung {model.Quantity} bản sao mới cho đầu sách \"{book.Title}\".");
        }, cancellationToken: ct);

    public Task<ServiceResult> UpdateBookCopyAsync(BookCopyUpdateViewModel model, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var copy = await unitOfWork.BookCopies.FirstOrDefaultAsync(
                c => c.Id == model.Id && c.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (copy == null)
            {
                return ServiceResult.Failure(ErrorCode.BookCopyNotFound);
            }

            if (model.ShelfId.HasValue &&
                !await unitOfWork.Shelves.ExistsAsync(s => s.Id == model.ShelfId.Value && s.Status != EntityStatus.Deleted, ct))
            {
                return ServiceResult.Failure(ErrorCode.ShelfNotFound);
            }

            copy.ShelfId = model.ShelfId;
            copy.Price = model.Price;
            copy.Note = model.Note?.Trim();

            if (model.RestoreToAvailable)
            {
                var guard = EnsureNotInCirculation(copy, "chỉnh sửa trạng thái");
                if (guard != null)
                {
                    return guard;
                }

                copy.CopyStatus = LibraryConstants.CopyStatus.Available;
                copy.DisposedAt = null;
                copy.DisposalReason = null;

                if (copy.Status == EntityStatus.Active)
                {
                    await reservationQueueEngine.AssignCopyToNextPendingOrReleaseAsync(copy, ct: ct);
                }
            }

            return ServiceResult.Success($"Đã cập nhật bản sao mã vạch {copy.Barcode}.");
        }, cancellationToken: ct);

    public Task<ServiceResult> DisposeOrMarkCopyAsync(BookCopyDisposeViewModel model, CancellationToken ct = default)
    {
        if (model.TargetStatus is not (LibraryConstants.CopyStatus.Damaged or LibraryConstants.CopyStatus.Lost or LibraryConstants.CopyStatus.Disposed))
        {
            return Task.FromResult(ServiceResult.Failure(ErrorCode.CommonInvalidRequest));
        }

        return unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var copy = await unitOfWork.BookCopies.FirstOrDefaultAsync(
                c => c.Id == model.CopyId && c.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (copy == null)
            {
                return ServiceResult.Failure(ErrorCode.BookCopyNotFound);
            }

            var inUse = await CheckCopyInCirculationAsync(copy, "thanh lý", ct);
            if (inUse != null)
            {
                return inUse;
            }

            copy.CopyStatus = model.TargetStatus;
            var reasonTrim = model.DisposalReason.Trim();
            if (model.TargetStatus == LibraryConstants.CopyStatus.Disposed)
            {
                copy.DisposedAt = dateTimeProvider.UtcNow;
                copy.DisposalReason = reasonTrim;
            }
            else
            {
                // Thỏa mãn ck_book_copies_dispose: ([copy_status]<>'DISPOSED' AND [disposed_at] IS NULL)
                copy.DisposedAt = null;
                copy.DisposalReason = null;
            }

            if (!string.IsNullOrWhiteSpace(model.Note))
            {
                copy.Note = model.Note.Trim();
            }
            else if (model.TargetStatus != LibraryConstants.CopyStatus.Disposed)
            {
                copy.Note = reasonTrim;
            }

            var actionName = model.TargetStatus switch
            {
                LibraryConstants.CopyStatus.Damaged => "ghi nhận hư hỏng",
                LibraryConstants.CopyStatus.Lost => "ghi nhận mất",
                _ => "thanh lý"
            };
            return ServiceResult.Success($"Đã {actionName} bản sao mã vạch {copy.Barcode}.");
        }, cancellationToken: ct);
    }

    public Task<ServiceResult> SoftDeleteBookCopyAsync(Guid id, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var copy = await unitOfWork.BookCopies.FirstOrDefaultAsync(
                c => c.Id == id && c.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (copy == null)
            {
                return ServiceResult.Failure(ErrorCode.BookCopyNotFound);
            }

            var inUse = await CheckCopyInCirculationAsync(copy, "xóa mềm", ct);
            if (inUse != null)
            {
                return inUse;
            }

            copy.Status = EntityStatus.Deleted;
            return ServiceResult.Success($"Đã xóa mềm bản sao mã vạch {copy.Barcode} (chuyển sang DELETED).");
        }, cancellationToken: ct);

    public Task<ServiceResult> RestoreBookCopyAsync(Guid id, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var copy = await unitOfWork.BookCopies.GetQueryable(asNoTracking: false)
                .Include(c => c.Book)
                .FirstOrDefaultAsync(c => c.Id == id && c.Status == EntityStatus.Deleted, ct);
            if (copy == null)
            {
                return ServiceResult.Failure(ErrorCode.BookCopyNotFound);
            }

            if (copy.Book.Status == EntityStatus.Deleted)
            {
                return ServiceResult.Failure(
                    ErrorCode.CommonRecordInUse,
                    $"Đầu sách \"{copy.Book.Title}\" của bản sao này đang ở trạng thái Đã xóa mềm. Vui lòng khôi phục đầu sách trước khi khôi phục bản sao.");
            }

            copy.Status = EntityStatus.Active;
            if (copy.CopyStatus == LibraryConstants.CopyStatus.Available && copy.Book.Status == EntityStatus.Active)
            {
                await reservationQueueEngine.AssignCopyToNextPendingOrReleaseAsync(copy, ct: ct);
            }

            return ServiceResult.Success($"Đã khôi phục bản sao mã vạch {copy.Barcode} về trạng thái hoạt động.");
        }, cancellationToken: ct);

    public Task<ServiceResult> HardDeleteBookCopyAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        if (!LibraryConstants.IsValidHardDeleteConfirm(confirmCode))
        {
            return Task.FromResult(ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid));
        }

        return unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var copy = await unitOfWork.BookCopies.FirstOrDefaultAsync(c => c.Id == id, asNoTracking: false, ct);
            if (copy == null)
            {
                return ServiceResult.Failure(ErrorCode.BookCopyNotFound);
            }

            if (copy.Status != EntityStatus.Deleted)
            {
                return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
            }

            var hasHistory = await unitOfWork.BorrowDetails.ExistsAsync(bd => bd.CopyId == id, ct) ||
                             await unitOfWork.Reservations.ExistsAsync(r => r.AssignedCopyId == id, ct);
            if (hasHistory)
            {
                return ServiceResult.Failure(
                    ErrorCode.CommonRecordInUse,
                    "Không thể xóa vĩnh viễn bản sao đã phát sinh lịch sử mượn trả hoặc giữ chỗ đặt trước.");
            }

            var barcode = copy.Barcode;
            unitOfWork.BookCopies.Delete(copy);
            return ServiceResult.Success($"Đã xóa vĩnh viễn bản sao mã vạch {barcode} khỏi cơ sở dữ liệu.");
        }, cancellationToken: ct);
    }

    // ===== Helpers nội bộ =====

    private static BookCopy BuildCopy(
        Guid bookId,
        Guid shelfId,
        string barcode,
        decimal? price,
        DateOnly acquiredDate,
        string? note) =>
        new()
        {
            Id = Guid.NewGuid(),
            BookId = bookId,
            ShelfId = shelfId,
            Barcode = barcode,
            CopyStatus = LibraryConstants.CopyStatus.Available,
            Price = price,
            AcquiredDate = acquiredDate,
            Note = note?.Trim(),
            Status = EntityStatus.Active
        };

    private static ServiceResult? EnsureNotInCirculation(BookCopy copy, string actionLabel)
    {
        if (copy.CopyStatus == LibraryConstants.CopyStatus.Borrowed)
        {
            return ServiceResult.Failure(ErrorCode.BookCopyCurrentlyBorrowed);
        }

        if (copy.CopyStatus == LibraryConstants.CopyStatus.Reserved)
        {
            return ServiceResult.Failure(
                ErrorCode.CommonRecordInUse,
                $"Bản sao mã vạch {copy.Barcode} đang được giữ chỗ (RESERVED) cho bạn đọc đặt trước. Vui lòng hủy phiếu đặt trước trước khi {actionLabel}.");
        }

        return null;
    }

    private async Task<ServiceResult?> CheckCopyInCirculationAsync(BookCopy copy, string actionLabel, CancellationToken ct)
    {
        if (copy.CopyStatus == LibraryConstants.CopyStatus.Borrowed ||
            await unitOfWork.BorrowDetails.ExistsAsync(
                bd => bd.CopyId == copy.Id && bd.ReturnedAt == null && bd.Status != EntityStatus.Deleted, ct))
        {
            return ServiceResult.Failure(ErrorCode.BookCopyCurrentlyBorrowed);
        }

        if (copy.CopyStatus == LibraryConstants.CopyStatus.Reserved ||
            await unitOfWork.Reservations.ExistsAsync(
                r => r.AssignedCopyId == copy.Id &&
                     r.Status != EntityStatus.Deleted &&
                     r.ReservationStatus == LibraryConstants.ReservationStatus.Ready, ct))
        {
            return ServiceResult.Failure(
                ErrorCode.CommonRecordInUse,
                $"Bản sao mã vạch {copy.Barcode} đang được giữ chỗ (RESERVED) cho bạn đọc đặt trước. Vui lòng hủy phiếu đặt trước trước khi {actionLabel}.");
        }

        return null;
    }
}
