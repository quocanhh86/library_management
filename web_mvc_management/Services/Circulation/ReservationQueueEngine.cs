using Microsoft.EntityFrameworkCore;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Infrastructure;
using web_mvc_management.Repositories;
using static web_mvc_management.Constant.LibraryConstants;

namespace web_mvc_management.Services;

/// <summary>
/// Triển khai duy nhất logic hàng đợi đặt trước FIFO (Copy Trap), hết hạn giữ chỗ (READY -> EXPIRED) và giải phóng bản sao.
/// </summary>
public class ReservationQueueEngine(
    IUnitOfWork unitOfWork,
    ICirculationPolicy circulationPolicy,
    IDateTimeProvider dateTimeProvider) : IReservationQueueEngine
{
    public async Task AssignCopyToNextPendingOrReleaseAsync(
        BookCopy copy,
        Guid? excludeReaderId = null,
        Guid? excludeReservationId = null,
        CancellationToken ct = default)
    {
        if (copy.Status != EntityStatus.Active)
        {
            return;
        }

        var now = dateTimeProvider.UtcNow;
        var today = dateTimeProvider.Today;

        var nextPending = await unitOfWork.Reservations.GetQueryable(asNoTracking: false)
            .Where(r => r.Status != EntityStatus.Deleted
                        && r.BookId == copy.BookId
                        && (excludeReaderId == null || r.ReaderId != excludeReaderId)
                        && (excludeReservationId == null || r.Id != excludeReservationId)
                        && r.Reader.Status == EntityStatus.Active
                        && r.Reader.CardStatus == CardStatus.Active
                        && r.Reader.CardExpiryDate >= today
                        && r.Book.Status == EntityStatus.Active
                        && r.ReservationStatus == ReservationStatus.Pending
                        && !r.Reader.BorrowSlips.Any(s => s.Status != EntityStatus.Deleted &&
                            s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null && d.BookCopy.BookId == copy.BookId)))
            .OrderBy(r => r.ReservedAt)
            .FirstOrDefaultAsync(ct);

        if (nextPending is null)
        {
            copy.CopyStatus = CopyStatus.Available;
        }
        else
        {
            var setting = await circulationPolicy.GetEffectiveSettingAsync(ct);
            nextPending.ReservationStatus = ReservationStatus.Ready;
            nextPending.AssignedCopyId = copy.Id;
            nextPending.ReadyAt = now;
            nextPending.ExpiresAt = now.AddHours(setting.ReservationHoldHours);
            copy.CopyStatus = CopyStatus.Reserved;
        }

        // Lưu ngay để lần chuyển kế tiếp trong cùng vòng lặp không chọn trùng cùng một phiếu đặt trước.
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ExpireOverdueHoldsAsync(CancellationToken ct = default)
    {
        var setting = await circulationPolicy.FindSettingAsync(ct);
        if (setting is null)
        {
            return;
        }

        var now = dateTimeProvider.UtcNow;
        var expired = await unitOfWork.Reservations.GetQueryable(asNoTracking: false)
            .Include(r => r.AssignedCopy)
            .Where(r => r.Status != EntityStatus.Deleted
                        && r.ReservationStatus == ReservationStatus.Ready
                        && r.ExpiresAt < now)
            .ToListAsync(ct);

        foreach (var reservation in expired)
        {
            reservation.ReservationStatus = ReservationStatus.Expired;
            reservation.ClosedAt = now;

            if (reservation.AssignedCopy is { CopyStatus: CopyStatus.Reserved } copy)
            {
                await AssignCopyToNextPendingOrReleaseAsync(copy, excludeReservationId: reservation.Id, ct: ct);
            }
            else
            {
                await unitOfWork.SaveChangesAsync(ct);
            }
        }
    }

    public async Task CancelReaderOpenReservationsAsync(Guid readerId, CancellationToken ct = default)
    {
        var activeReservations = await unitOfWork.Reservations.GetQueryable(asNoTracking: false)
            .Include(r => r.AssignedCopy)
            .Where(r => r.ReaderId == readerId
                        && r.Status != EntityStatus.Deleted
                        && (r.ReservationStatus == ReservationStatus.Pending ||
                            r.ReservationStatus == ReservationStatus.Ready))
            .ToListAsync(ct);

        if (activeReservations.Count == 0)
        {
            return;
        }

        var now = dateTimeProvider.UtcNow;
        foreach (var res in activeReservations)
        {
            var wasReady = res.ReservationStatus == ReservationStatus.Ready;
            res.ReservationStatus = ReservationStatus.Cancelled;
            res.ClosedAt = now;

            if (wasReady && res.AssignedCopy is { CopyStatus: CopyStatus.Reserved } heldCopy)
            {
                await AssignCopyToNextPendingOrReleaseAsync(heldCopy, excludeReaderId: readerId, excludeReservationId: res.Id, ct: ct);
            }
        }
    }
}
