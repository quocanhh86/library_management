using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Helper;
using web_mvc_management.Infrastructure;
using web_mvc_management.Repositories;
using static web_mvc_management.Constant.LibraryConstants;

namespace web_mvc_management.Services;

/// <summary>
/// Triển khai tập trung chính sách mượn, trả, gia hạn và đặt trước thống nhất giữa Thủ thư và Độc giả.
/// </summary>
public class CirculationPolicy(IUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider) : ICirculationPolicy
{
    public Task<LibrarySetting?> FindSettingAsync(CancellationToken ct = default) =>
        unitOfWork.LibrarySettings.GetQueryable(asNoTracking: true)
            .Where(s => s.Status != EntityStatus.Deleted)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<LibrarySetting> GetEffectiveSettingAsync(CancellationToken ct = default)
    {
        var setting = await FindSettingAsync(ct);
        return setting ?? new LibrarySetting
        {
            OverdueFinePerDay = Defaults.OverdueFinePerDay,
            DamagedFinePercent = Defaults.DamagedFinePercent,
            LostFinePercent = Defaults.LostFinePercent,
            MaxUnpaidFine = Defaults.MaxUnpaidFine,
            ReservationHoldHours = Defaults.ReservationHoldHours
        };
    }

    public ErrorCode? GetCardBlockingError(Reader reader, DateOnly today) =>
        reader.CardStatus == CardStatus.Locked ? ErrorCode.ReaderCardLocked
        : reader.CardStatus == CardStatus.Expired || reader.CardExpiryDate < today ? ErrorCode.ReaderCardExpired
        : null;

    public Task<decimal> GetUnpaidFinesAsync(Guid readerId, CancellationToken ct = default) =>
        unitOfWork.Fines.GetQueryable(asNoTracking: true)
            .Where(f => f.Status != EntityStatus.Deleted
                        && f.FineStatus == FineStatus.Unpaid
                        && f.BorrowDetail.Status != EntityStatus.Deleted
                        && f.BorrowDetail.BorrowSlip.Status != EntityStatus.Deleted
                        && f.BorrowDetail.BorrowSlip.ReaderId == readerId)
            .SumAsync(f => f.Amount, ct);

    public async Task<ReaderEligibilityResult> EvaluateReaderAsync(Reader reader, CancellationToken ct = default)
    {
        var today = dateTimeProvider.Today;
        var reasons = new List<string>();
        ErrorCode? firstCode = null;

        if (GetCardBlockingError(reader, today) is { } cardError)
        {
            firstCode ??= cardError;
            reasons.Add(ErrorMessages.Get(cardError));
        }

        var dueDates = await OpenLoansQuery(reader.Id)
            .Select(d => d.DueDate)
            .ToListAsync(ct);

        var hasOverdue = dueDates.Any(d => d < today);
        if (hasOverdue)
        {
            firstCode ??= ErrorCode.ReaderHasOverdueBooks;
            reasons.Add(ErrorMessages.Get(ErrorCode.ReaderHasOverdueBooks));
        }

        var setting = await GetEffectiveSettingAsync(ct);
        var unpaidFines = await GetUnpaidFinesAsync(reader.Id, ct);
        if (unpaidFines >= setting.MaxUnpaidFine)
        {
            firstCode ??= ErrorCode.ReaderHasUnpaidFines;
            reasons.Add(ErrorMessages.Format(ErrorCode.ReaderHasUnpaidFines, CurrencyHelper.ToVnd(unpaidFines)));
        }

        if (dueDates.Count >= reader.ReaderType.MaxBorrowBooks)
        {
            firstCode ??= ErrorCode.ReaderMaxBorrowLimitExceeded;
            reasons.Add(ErrorMessages.Format(ErrorCode.ReaderMaxBorrowLimitExceeded, reader.ReaderType.MaxBorrowBooks));
        }

        return new ReaderEligibilityResult(reasons, dueDates.Count, hasOverdue, unpaidFines, setting.MaxUnpaidFine, firstCode);
    }

    public async Task<ServiceResult> ValidateRenewAsync(BorrowDetail detail, CancellationToken ct = default)
    {
        if (detail.ReturnedAt != null)
        {
            return ServiceResult.Failure(ErrorCode.BookAlreadyReturned);
        }

        if (detail.BookCopy.Status != EntityStatus.Active || detail.BookCopy.Book.Status != EntityStatus.Active)
        {
            return ServiceResult.Failure(ErrorCode.BookCopyNotAvailable);
        }

        var today = dateTimeProvider.Today;
        var reader = detail.BorrowSlip.Reader;
        if (reader.Status != EntityStatus.Active)
        {
            return ServiceResult.Failure(ErrorCode.AuthAccountInactive);
        }

        if (GetCardBlockingError(reader, today) is { } cardError)
        {
            return ServiceResult.Failure(cardError);
        }

        if (detail.DueDate < today)
        {
            return ServiceResult.Failure(ErrorCode.RenewOverdueNotAllowed);
        }

        var readerId = detail.BorrowSlip.ReaderId;
        if (await OpenLoansQuery(readerId).AnyAsync(d => d.DueDate < today, ct))
        {
            return ServiceResult.Failure(ErrorCode.ReaderHasOverdueBooks);
        }

        var setting = await GetEffectiveSettingAsync(ct);
        var unpaid = await GetUnpaidFinesAsync(readerId, ct);
        if (unpaid >= setting.MaxUnpaidFine)
        {
            return ServiceResult.Failure(
                ErrorCode.ReaderHasUnpaidFines,
                ErrorMessages.Format(ErrorCode.ReaderHasUnpaidFines, CurrencyHelper.ToVnd(unpaid)));
        }

        if (detail.RenewCount >= reader.ReaderType.MaxRenewals)
        {
            return ServiceResult.Failure(
                ErrorCode.RenewalLimitReached,
                ErrorMessages.Format(ErrorCode.RenewalLimitReached, reader.ReaderType.MaxRenewals));
        }

        // Chỉ người đang chờ (PENDING) mới cần bản sao này; người READY đã được giữ bản khác.
        var someoneWaiting = await unitOfWork.Reservations.GetQueryable(asNoTracking: true)
            .AnyAsync(r => r.Status != EntityStatus.Deleted
                           && r.BookId == detail.BookCopy.BookId
                           && r.ReaderId != readerId
                           && r.ReservationStatus == ReservationStatus.Pending, ct);

        if (someoneWaiting)
        {
            return ServiceResult.Failure(ErrorCode.RenewBlockedByReservation);
        }

        return ServiceResult.Success();
    }

    public async Task<ServiceResult<ReservationEligibilityContext>> ValidateReservationAsync(
        Guid readerId,
        Guid bookId,
        CancellationToken ct = default)
    {
        var today = dateTimeProvider.Today;
        var reader = await unitOfWork.Readers.GetQueryable(asNoTracking: true)
            .Include(r => r.ReaderType)
            .FirstOrDefaultAsync(r => r.Id == readerId && r.Status == EntityStatus.Active, ct);

        if (reader is null)
        {
            return ServiceResult<ReservationEligibilityContext>.Failure(ErrorCode.ReaderNotFound);
        }

        if (GetCardBlockingError(reader, today) is { } cardError)
        {
            return ServiceResult<ReservationEligibilityContext>.Failure(cardError);
        }

        if (await OpenLoansQuery(readerId).AnyAsync(d => d.DueDate < today, ct))
        {
            return ServiceResult<ReservationEligibilityContext>.Failure(ErrorCode.ReaderHasOverdueBooks);
        }

        var setting = await GetEffectiveSettingAsync(ct);
        var unpaid = await GetUnpaidFinesAsync(readerId, ct);
        if (unpaid >= setting.MaxUnpaidFine)
        {
            return ServiceResult<ReservationEligibilityContext>.Failure(
                ErrorCode.ReaderHasUnpaidFines,
                ErrorMessages.Format(ErrorCode.ReaderHasUnpaidFines, CurrencyHelper.ToVnd(unpaid)));
        }

        var book = await unitOfWork.Books.GetQueryable(asNoTracking: true)
            .Where(b => b.Id == bookId && b.Status == EntityStatus.Active)
            .Select(b => new
            {
                b.Title,
                HasAvailable = b.BookCopies.Any(c => c.Status != EntityStatus.Deleted && c.CopyStatus == CopyStatus.Available),
                HasCirculating = b.BookCopies.Any(c => c.Status != EntityStatus.Deleted
                                                       && (c.CopyStatus == CopyStatus.Borrowed || c.CopyStatus == CopyStatus.Reserved))
            })
            .FirstOrDefaultAsync(ct);

        if (book is null)
        {
            return ServiceResult<ReservationEligibilityContext>.Failure(ErrorCode.BookNotFound);
        }

        if (book.HasAvailable)
        {
            return ServiceResult<ReservationEligibilityContext>.Failure(ErrorCode.ReservationBookAvailable);
        }

        if (!book.HasCirculating)
        {
            return ServiceResult<ReservationEligibilityContext>.Failure(ErrorCode.ReservationNotAvailable);
        }

        if (await OpenLoansQuery(readerId).AnyAsync(d => d.BookCopy.BookId == bookId, ct))
        {
            return ServiceResult<ReservationEligibilityContext>.Failure(ErrorCode.ReaderAlreadyBorrowingBook);
        }

        var duplicate = await unitOfWork.Reservations.GetQueryable(asNoTracking: true)
            .AnyAsync(r => r.Status != EntityStatus.Deleted
                           && r.ReaderId == readerId
                           && r.BookId == bookId
                           && (r.ReservationStatus == ReservationStatus.Pending || r.ReservationStatus == ReservationStatus.Ready), ct);

        if (duplicate)
        {
            return ServiceResult<ReservationEligibilityContext>.Failure(ErrorCode.ReservationDuplicate);
        }

        return ServiceResult<ReservationEligibilityContext>.Success(
            new ReservationEligibilityContext(reader.FullName, book.Title));
    }

    private IQueryable<BorrowDetail> OpenLoansQuery(Guid readerId) =>
        unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .Where(d => d.Status != EntityStatus.Deleted
                        && d.ReturnedAt == null
                        && d.BorrowSlip.Status != EntityStatus.Deleted
                        && d.BorrowSlip.ReaderId == readerId);
}
