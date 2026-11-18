using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Extensions;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Circulation;
using web_mvc_management.Repositories;
using static web_mvc_management.Constant.LibraryConstants;

namespace web_mvc_management.Services;

public class ReservationService(
    IUnitOfWork unitOfWork,
    ICirculationPolicy circulationPolicy,
    IReservationQueueEngine reservationQueueEngine,
    IDateTimeProvider dateTimeProvider) : IReservationService
{
    public async Task<ReservationsPageViewModel> GetReservationsPageAsync(ReservationFilter filter, CancellationToken ct = default)
    {
        await reservationQueueEngine.ExpireOverdueHoldsAsync(ct);

        var query = unitOfWork.Reservations.GetQueryable(asNoTracking: true)
            .Where(r => r.Status != EntityStatus.Deleted);

        if (!string.IsNullOrWhiteSpace(filter.ReservationStatus))
        {
            query = query.Where(r => r.ReservationStatus == filter.ReservationStatus);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.ToLower();
            query = query.Where(r =>
                r.Reader.FullName.ToLower().Contains(kw) ||
                r.Reader.CardNo.ToLower().Contains(kw) ||
                r.Book.Title.ToLower().Contains(kw));
        }

        var paged = await query
            .OrderByDescending(r => r.ReservedAt)
            .Select(r => new ReservationItemViewModel
            {
                Id = r.Id,
                ReaderName = r.Reader.FullName,
                CardNo = r.Reader.CardNo,
                BookTitle = r.Book.Title,
                ReservationStatus = r.ReservationStatus,
                ReservedAt = r.ReservedAt,
                ExpiresAt = r.ExpiresAt,
                AssignedBarcode = r.AssignedCopy != null ? r.AssignedCopy.Barcode : null,
                QueuePosition = r.ReservationStatus == ReservationStatus.Pending
                    ? r.Book.Reservations.Count(o => o.Status != EntityStatus.Deleted
                                                     && o.ReservationStatus == ReservationStatus.Pending
                                                     && o.ReservedAt < r.ReservedAt) + 1
                    : 0
            })
            .ToPagedResultAsync(filter, ct);

        // Chỉ đặt trước được đầu sách hết bản sẵn sàng nhưng còn bản đang mượn / đang giữ.
        var books = await unitOfWork.Books.GetQueryable(asNoTracking: true)
            .Where(b => b.Status != EntityStatus.Deleted
                        && !b.BookCopies.Any(c => c.Status != EntityStatus.Deleted && c.CopyStatus == CopyStatus.Available)
                        && b.BookCopies.Any(c => c.Status != EntityStatus.Deleted
                                                 && (c.CopyStatus == CopyStatus.Borrowed || c.CopyStatus == CopyStatus.Reserved)))
            .OrderBy(b => b.Title)
            .Select(b => new BookOption(b.Id, b.Title))
            .ToListAsync(ct);

        return new ReservationsPageViewModel
        {
            Reservations = paged,
            Filter = filter,
            ReservableBooks = books
        };
    }

    public Task<ServiceResult> CreateReservationAsync(ReservationCreateViewModel model, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            await reservationQueueEngine.ExpireOverdueHoldsAsync(ct);

            var cardNo = model.CardNo.Trim();
            var reader = await unitOfWork.Readers.GetQueryable(asNoTracking: true)
                .FirstOrDefaultAsync(r => r.CardNo == cardNo && r.Status == EntityStatus.Active, ct);

            if (reader is null)
            {
                return ServiceResult.Failure(ErrorCode.ReaderNotFound);
            }

            var validation = await circulationPolicy.ValidateReservationAsync(reader.Id, model.BookId, ct);
            if (!validation.IsSuccess || validation.Data is null)
            {
                return ServiceResult.Failure(validation.Code ?? ErrorCode.ReservationNotAvailable, validation.Message);
            }

            await unitOfWork.Reservations.AddAsync(new Reservation
            {
                Id = Guid.NewGuid(),
                ReaderId = reader.Id,
                BookId = model.BookId,
                ReservedAt = dateTimeProvider.UtcNow
            }, ct);

            return ServiceResult.Success($"Đã đặt trước \"{validation.Data.BookTitle}\" cho {validation.Data.ReaderFullName}.");
        }, cancellationToken: ct);

    public Task<ServiceResult> CancelReservationAsync(Guid id, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var reservation = await unitOfWork.Reservations.GetQueryable(asNoTracking: false)
                .Include(r => r.AssignedCopy)
                .FirstOrDefaultAsync(r => r.Id == id && r.Status != EntityStatus.Deleted, ct);

            if (reservation is null)
            {
                return ServiceResult.Failure(ErrorCode.ReservationNotFound);
            }

            if (reservation.ReservationStatus is not (ReservationStatus.Pending or ReservationStatus.Ready))
            {
                return ServiceResult.Failure(ErrorCode.ReservationAlreadyClosed);
            }

            var wasReady = reservation.ReservationStatus == ReservationStatus.Ready;
            reservation.ReservationStatus = ReservationStatus.Cancelled;
            reservation.ClosedAt = dateTimeProvider.UtcNow;

            if (wasReady && reservation.AssignedCopy is { CopyStatus: CopyStatus.Reserved } copy)
            {
                await reservationQueueEngine.AssignCopyToNextPendingOrReleaseAsync(
                    copy,
                    excludeReservationId: reservation.Id,
                    ct: ct);
            }

            return ServiceResult.Success("Đã hủy phiếu đặt trước.");
        }, cancellationToken: ct);
}
