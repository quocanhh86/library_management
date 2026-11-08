using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Extensions;
using web_mvc_management.Helper;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Circulation;
using web_mvc_management.Models.ViewModels.Home;
using web_mvc_management.Repositories;
using static web_mvc_management.Constant.LibraryConstants;

namespace web_mvc_management.Services;

/// <summary>
/// Nghiá»‡p vá»¥ mÆ°á»£n â€“ tráº£ táº¡i quáº§y, Ä‘á»“ng thá»i cung cáº¥p dá»¯ liá»‡u "sÃ¡ch Ä‘ang mÆ°á»£n" cho trang chá»§ Ä‘á»™c giáº£.
/// </summary>
public class CirculationService(
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ICirculationService
{
    // ===== Quáº§y mÆ°á»£n / tráº£ =====

    public async Task<CirculationDeskViewModel> GetDeskAsync(bool returning, string? card, CancellationToken ct = default)
    {
        var today = dateTimeProvider.Today;
        var setting = await GetSettingAsync(ct);
        if (!returning) await ExpireHoldsAsync(setting, ct);

        var cardNo = card?.Trim();
        var reader = string.IsNullOrEmpty(cardNo)
            ? null
            : await Readers(asNoTracking: true).FirstOrDefaultAsync(r => r.CardNo == cardNo, ct);

        if (reader is null)
        {
            return new CirculationDeskViewModel
            {
                Returning = returning,
                Today = today,
                Card = cardNo,
                FinePerDay = setting?.OverdueFinePerDay ?? 0,
                Error = string.IsNullOrEmpty(cardNo) ? null : ErrorMessages.Get(ErrorCode.ReaderNotFound)
            };
        }

        var (reasons, borrowed, unpaid) = await CheckBorrowerAsync(reader, ct);
        var finePerDay = setting?.OverdueFinePerDay ?? 0;

        var loans = await OpenLoans(reader.Id)
            .OrderBy(d => d.DueDate)
            .Select(d => new
            {
                d.Id,
                d.BookCopy.Book.Title,
                d.BookCopy.Barcode,
                d.BorrowSlip.BorrowedAt,
                d.DueDate,
                d.RenewCount,
                ChargedDays = d.Fines
                    .Where(f => f.Status != EntityStatus.Deleted && f.FineType == FineType.Overdue && f.FineStatus != FineStatus.Cancelled)
                    .Sum(f => f.OverdueDays ?? 0)
            })
            .ToListAsync(ct);

        var holds = await unitOfWork.Reservations.GetQueryable(asNoTracking: true)
            .Where(r => r.ReaderId == reader.Id && r.Status != EntityStatus.Deleted && r.ReservationStatus == ReservationStatus.Ready)
            .OrderBy(r => r.ExpiresAt)
            .Select(r => new DeskHold(r.Book.Title, r.AssignedCopy != null ? r.AssignedCopy.Barcode : "", r.ExpiresAt))
            .ToListAsync(ct);

        var cardStatus = CardProblem(reader, today) == ErrorCode.ReaderCardExpired ? CardStatus.Expired : reader.CardStatus;

        return new CirculationDeskViewModel
        {
            Returning = returning,
            Today = today,
            Card = cardNo,
            FinePerDay = finePerDay,
            LoanDays = reader.ReaderType.LoanDays,
            MaxRenewals = reader.ReaderType.MaxRenewals,
            Reader = new DeskReader(reader.Id, reader.FullName, reader.CardNo, reader.ReaderType.Name, reader.CardExpiryDate,
                cardStatus, borrowed, reader.ReaderType.MaxBorrowBooks, unpaid),
            BlockReasons = reasons,
            Holds = holds,
            Loans = loans
                .Select(l => new DeskLoan(l.Id, l.Title, l.Barcode, l.BorrowedAt, l.DueDate, l.RenewCount,
                    Math.Max(0, today.DayNumber - l.DueDate.DayNumber - l.ChargedDays) * finePerDay))
                .ToList()
        };
    }

    public async Task<ServiceResult<Guid>> CreateBorrowSlipAsync(BorrowSubmitViewModel model, CancellationToken ct = default)
    {
        if (userContext.CurrentUserId is not { } staffId) return ServiceResult<Guid>.Failure(ErrorCode.AuthUnauthorized);

        var barcodes = model.Barcodes.Select(b => b?.Trim() ?? "").Where(b => b.Length > 0).ToList();
        if (barcodes.Count == 0) return ServiceResult<Guid>.ValidationError(ErrorCode.BorrowListEmpty);

        var duplicate = barcodes.GroupBy(b => b, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            return ServiceResult<Guid>.ValidationError(ErrorCode.BorrowDuplicateBarcode,
                ErrorMessages.Format(ErrorCode.BorrowDuplicateBarcode, duplicate.Key));
        }

        await ExpireHoldsAsync(await GetSettingAsync(ct), ct);

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var reader = await Readers(asNoTracking: true).FirstOrDefaultAsync(r => r.Id == model.ReaderId && r.Status == EntityStatus.Active, ct);
            if (reader is null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult<Guid>.Failure(ErrorCode.ReaderNotFound);
            }

            var (reasons, borrowed, _) = await CheckBorrowerAsync(reader, ct);
            if (reasons.Count > 0)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult<Guid>.Failure(reasons[0]);
            }

            var limit = reader.ReaderType.MaxBorrowBooks;
            if (borrowed + barcodes.Count > limit)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult<Guid>.Failure(ErrorCode.ReaderMaxBorrowLimitExceeded,
                    ErrorMessages.Format(ErrorCode.ReaderMaxBorrowLimitExceeded, limit));
            }

            var copies = await unitOfWork.BookCopies.GetQueryable(asNoTracking: false)
                .Include(c => c.Book)
                .Where(c => c.Status == EntityStatus.Active && c.Book.Status == EntityStatus.Active && barcodes.Contains(c.Barcode))
                .ToListAsync(ct);
            var copyIds = copies.Select(c => c.Id).ToList();
            var holds = await unitOfWork.Reservations.GetQueryable(asNoTracking: false)
                .Where(r => r.Status != EntityStatus.Deleted && r.ReservationStatus == ReservationStatus.Ready
                            && r.AssignedCopyId != null && copyIds.Contains(r.AssignedCopyId.Value))
                .ToListAsync(ct);

            var now = dateTimeProvider.UtcNow;
            var picked = new List<BookCopy>();
            foreach (var code in barcodes)
            {
                var copy = copies.FirstOrDefault(c => string.Equals(c.Barcode, code, StringComparison.OrdinalIgnoreCase));
                if (copy is null)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return CopyFailure<Guid>(ErrorCode.BookCopyNotFound, code);
                }

                switch (copy.CopyStatus)
                {
                    case CopyStatus.Available:
                        break;
                    case CopyStatus.Reserved:
                        var hold = holds.FirstOrDefault(h => h.AssignedCopyId == copy.Id);
                        if (hold is null || hold.ReaderId != reader.Id)
                        {
                            await unitOfWork.RollbackTransactionAsync(ct);
                            return ServiceResult<Guid>.Failure(ErrorCode.BookCopyReservedForOther,
                                ErrorMessages.Format(ErrorCode.BookCopyReservedForOther, copy.Barcode));
                        }
                        hold.ReservationStatus = ReservationStatus.Fulfilled;
                        hold.ClosedAt = now;
                        break;
                    case CopyStatus.Damaged or CopyStatus.Lost:
                        await unitOfWork.RollbackTransactionAsync(ct);
                        return CopyFailure<Guid>(ErrorCode.BookCopyDamagedOrLost, copy.Barcode);
                    default:
                        await unitOfWork.RollbackTransactionAsync(ct);
                        return CopyFailure<Guid>(ErrorCode.BookCopyNotAvailable, copy.Barcode);
                }
                picked.Add(copy);
            }

            var slip = new BorrowSlip
            {
                Id = Guid.NewGuid(),
                ReaderId = reader.Id,
                IssuedBy = staffId,
                BorrowedAt = now,
                Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim()
            };
            await unitOfWork.BorrowSlips.AddAsync(slip, ct);

            var dueDate = dateTimeProvider.Today.AddDays(reader.ReaderType.LoanDays);
            foreach (var copy in picked)
            {
                await unitOfWork.BorrowDetails.AddAsync(new BorrowDetail
                {
                    Id = Guid.NewGuid(),
                    SlipId = slip.Id,
                    CopyId = copy.Id,
                    DueDate = dueDate
                }, ct);
            }

            await unitOfWork.CommitTransactionAsync(ct);

            return ServiceResult<Guid>.Success(
                slip.Id,
                $"ÄÃ£ láº­p phiáº¿u mÆ°á»£n {picked.Count} cuá»‘n cho {reader.FullName}. Háº¡n tráº£ {dueDate.ToString(AppConstants.DefaultDateFormat)}.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> ReturnAsync(ReturnSubmitViewModel model, CancellationToken ct = default)
    {
        if (userContext.CurrentUserId is not { } staffId) return ServiceResult.Failure(ErrorCode.AuthUnauthorized);

        var items = model.Items.Where(i => i.Selected).DistinctBy(i => i.DetailId).ToList();
        if (items.Count == 0) return ServiceResult.ValidationError(ErrorCode.CommonInvalidRequest);
        if (items.Any(i => i.Condition is not (ReturnCondition.Good or ReturnCondition.Damaged or ReturnCondition.Lost)))
        {
            return ServiceResult.ValidationError(ErrorCode.ReturnConditionInvalid);
        }

        var setting = await GetSettingAsync(ct);
        if (setting is null) return ServiceResult.Failure(ErrorCode.LibrarySettingNotFound);

        var ids = items.Select(i => i.DetailId).ToList();
        var now = dateTimeProvider.UtcNow;
        var today = dateTimeProvider.Today;
        var totalFine = 0m;

        // Má»—i láº§n tráº£ GOOD cÃ³ thá»ƒ lÆ°u ngay Ä‘á»ƒ chuyá»ƒn báº£n sao cho ngÆ°á»i Ä‘áº·t káº¿ tiáº¿p, nÃªn gÃ³i cáº£ lÆ°á»£t trong transaction kÃ¨m lock.
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var details = await unitOfWork.BorrowDetails.GetQueryable(asNoTracking: false)
                .Include(d => d.BookCopy)
                .Include(d => d.BorrowSlip)
                .Include(d => d.Fines)
                .Where(d => ids.Contains(d.Id) && d.Status != EntityStatus.Deleted)
                .ToListAsync(ct);

            if (details.Count != ids.Count || details.Any(d => d.BorrowSlip.ReaderId != model.ReaderId))
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.BorrowDetailNotFound);
            }
            if (details.Any(d => d.ReturnedAt != null))
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.BookAlreadyReturned);
            }

            foreach (var item in items)
            {
                var detail = details.First(d => d.Id == item.DetailId);
                detail.ReturnedAt = now;
                detail.ReceivedBy = staffId;
                detail.ReturnCondition = item.Condition;
                detail.ReturnNote = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim();

                // Trá»« sá»‘ ngÃ y Ä‘Ã£ láº­p pháº¡t trÆ°á»›c Ä‘Ã³ cho dÃ²ng mÆ°á»£n nÃ y Ä‘á»ƒ khÃ´ng thu trÃ¹ng.
                var chargedDays = detail.Fines
                    .Where(f => f.Status != EntityStatus.Deleted && f.FineType == FineType.Overdue && f.FineStatus != FineStatus.Cancelled)
                    .Sum(f => f.OverdueDays ?? 0);
                var lateDays = today.DayNumber - detail.DueDate.DayNumber - chargedDays;
                if (lateDays > 0)
                {
                    totalFine += await AddFineAsync(detail, FineType.Overdue, lateDays * setting.OverdueFinePerDay, lateDays, ct);
                }

                if (item.Condition == ReturnCondition.Good)
                {
                    await ReleaseCopyAsync(detail.BookCopy, setting.ReservationHoldHours, ct);
                    continue;
                }

                var damaged = item.Condition == ReturnCondition.Damaged;
                detail.BookCopy.CopyStatus = damaged ? CopyStatus.Damaged : CopyStatus.Lost;
                if (detail.BookCopy.Price is { } price)
                {
                    var percent = damaged ? setting.DamagedFinePercent : setting.LostFinePercent;
                    totalFine += await AddFineAsync(detail, damaged ? FineType.Damaged : FineType.Lost,
                        Math.Round(price * percent / 100m, 0), null, ct);
                }
                else
                {
                    detail.ReturnNote = string.Join(" ", detail.ReturnNote, "TODO(xÃ¡c nháº­n): báº£n sao chÆ°a cÃ³ giÃ¡ nÃªn chÆ°a tÃ­nh pháº¡t.").Trim();
                }
            }

            await unitOfWork.CommitTransactionAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }

        var message = $"ÄÃ£ nháº­n tráº£ {items.Count} cuá»‘n.";
        if (totalFine > 0) message += $" PhÃ¡t sinh pháº¡t {CurrencyHelper.ToVnd(totalFine)}, thu táº¡i mÃ n Thu pháº¡t.";
        return ServiceResult.Success(message);
    }

    public async Task<ServiceResult> RenewAsync(Guid detailId, CancellationToken ct = default)
    {
        var detail = await unitOfWork.BorrowDetails.GetQueryable(asNoTracking: false)
            .Include(d => d.BookCopy)
            .Include(d => d.BorrowSlip).ThenInclude(s => s.Reader).ThenInclude(r => r.ReaderType)
            .FirstOrDefaultAsync(d => d.Id == detailId && d.Status != EntityStatus.Deleted, ct);

        if (detail is null) return ServiceResult.Failure(ErrorCode.BorrowDetailNotFound);
        if (detail.ReturnedAt != null) return ServiceResult.Failure(ErrorCode.BookAlreadyReturned);
        if (detail.DueDate < dateTimeProvider.Today) return ServiceResult.Failure(ErrorCode.RenewOverdueNotAllowed);

        var type = detail.BorrowSlip.Reader.ReaderType;
        if (detail.RenewCount >= type.MaxRenewals)
        {
            return ServiceResult.Failure(ErrorCode.RenewalLimitReached, ErrorMessages.Format(ErrorCode.RenewalLimitReached, type.MaxRenewals));
        }

        // Chá»‰ ngÆ°á»i Ä‘ang chá» (PENDING) má»›i cáº§n báº£n sao nÃ y; ngÆ°á»i READY Ä‘Ã£ Ä‘Æ°á»£c giá»¯ báº£n khÃ¡c.
        var readerId = detail.BorrowSlip.ReaderId;
        var someoneWaiting = await unitOfWork.Reservations.GetQueryable(asNoTracking: true)
            .AnyAsync(r => r.Status != EntityStatus.Deleted && r.BookId == detail.BookCopy.BookId
                           && r.ReaderId != readerId && r.ReservationStatus == ReservationStatus.Pending, ct);
        if (someoneWaiting) return ServiceResult.Failure(ErrorCode.RenewBlockedByReservation);

        detail.DueDate = detail.DueDate.AddDays(type.RenewDays);
        detail.RenewCount++;
        await unitOfWork.SaveChangesAsync(ct);

        return ServiceResult.Success(
            $"ÄÃ£ gia háº¡n Ä‘áº¿n {detail.DueDate.ToString(AppConstants.DefaultDateFormat)}. CÃ²n {type.MaxRenewals - detail.RenewCount} láº§n gia háº¡n.");
    }

    // ===== Quáº£n lÃ½ phiáº¿u mÆ°á»£n & Lá»‹ch sá»­ =====

    public async Task<BorrowHistoryPageViewModel> GetBorrowHistoryPageAsync(BorrowSlipFilter filter, CancellationToken ct = default)
    {
        var today = dateTimeProvider.Today;
        var baseQuery = unitOfWork.BorrowSlips.GetQueryable(asNoTracking: true)
            .Where(s => s.Status != EntityStatus.Deleted);

        var totalActive = await baseQuery.CountAsync(s => s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null), ct);
        var totalOverdue = await baseQuery.CountAsync(s => s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null && d.DueDate < today), ct);
        var totalReturned = await baseQuery.CountAsync(s =>
            s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted) &&
            !s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null), ct);

        var query = baseQuery;

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim();
            query = query.Where(s =>
                s.Reader.FullName.Contains(kw) ||
                s.Reader.CardNo.Contains(kw) ||
                s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted &&
                    (d.BookCopy.Barcode.Contains(kw) || d.BookCopy.Book.Title.Contains(kw))));
        }

        if (filter.FromDate.HasValue)
        {
            var fromOffset = DateTimeHelper.UtcDayBoundary(filter.FromDate.Value, dateTimeProvider.Now.Offset);
            query = query.Where(s => s.BorrowedAt >= fromOffset);
        }

        if (filter.ToDate.HasValue)
        {
            var toOffset = DateTimeHelper.UtcDayBoundary(filter.ToDate.Value, dateTimeProvider.Now.Offset, endOfDay: true);
            query = query.Where(s => s.BorrowedAt <= toOffset);
        }

        if (!string.IsNullOrWhiteSpace(filter.BorrowStatus))
        {
            switch (filter.BorrowStatus.ToLowerInvariant())
            {
                case "borrowing":
                    query = query.Where(s => s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null));
                    break;
                case "overdue":
                    query = query.Where(s => s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null && d.DueDate < today));
                    break;
                case "returned":
                    query = query.Where(s =>
                        s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted) &&
                        !s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null));
                    break;
            }
        }

        var totalCount = await query.CountAsync(ct);
        var safePageItemSize = filter.PageItemSize <= 0 ? PaginationParams.DefaultPageItemSize : filter.PageItemSize;
        var normalizedPageNo = filter.PageNo <= 1 ? 1 : filter.PageNo;
        var skip = PaginationParams.CalculateSkip(normalizedPageNo, safePageItemSize);

        var rawItems = await query
            .OrderByDescending(s => s.BorrowedAt)
            .Skip(skip)
            .Take(safePageItemSize)
            .Select(s => new
            {
                s.Id,
                s.ReaderId,
                ReaderName = s.Reader.FullName,
                s.Reader.CardNo,
                ReaderTypeName = s.Reader.ReaderType.Name,
                s.BorrowedAt,
                IssuedByStaffName = s.IssuedByStaff.FullName,
                s.Note,
                Details = s.BorrowDetails.Where(d => d.Status != EntityStatus.Deleted).Select(d => new
                {
                    d.Id,
                    d.DueDate,
                    d.ReturnedAt,
                    BookTitle = d.BookCopy.Book.Title
                }).ToList()
            })
            .ToListAsync(ct);

        var list = rawItems.Select(s =>
        {
            var totalBooks = s.Details.Count;
            var returnedBooks = s.Details.Count(d => d.ReturnedAt != null);
            var hasOverdue = s.Details.Any(d => d.ReturnedAt == null && d.DueDate < today);
            var status = (totalBooks > 0 && returnedBooks == totalBooks) ? "ÄÃ£ tráº£ Ä‘á»§" : (hasOverdue ? "CÃ³ sÃ¡ch quÃ¡ háº¡n" : "Äang mÆ°á»£n");
            return new BorrowSlipItemViewModel
            {
                Id = s.Id,
                SlipCode = $"PM-{s.BorrowedAt:yyyyMMdd}-{s.Id.ToString()[..4].ToUpper()}",
                ReaderId = s.ReaderId,
                ReaderName = s.ReaderName,
                CardNo = s.CardNo,
                ReaderTypeName = s.ReaderTypeName,
                BorrowedAt = s.BorrowedAt,
                IssuedByStaffName = s.IssuedByStaffName,
                TotalBooks = totalBooks,
                ReturnedBooks = returnedBooks,
                HasOverdue = hasOverdue,
                Status = status,
                Note = s.Note,
                BookTitles = s.Details.Select(d => d.BookTitle).ToList()
            };
        }).ToList();

        var pagedResult = new PagedResult<BorrowSlipItemViewModel>(list, totalCount, normalizedPageNo, safePageItemSize);

        return new BorrowHistoryPageViewModel
        {
            Slips = pagedResult,
            Filter = filter,
            TotalActiveSlips = totalActive,
            TotalOverdueSlips = totalOverdue,
            TotalReturnedSlips = totalReturned
        };
    }

    public async Task<BorrowSlipDetailViewModel?> GetBorrowSlipDetailAsync(Guid id, CancellationToken ct = default)
    {
        var today = dateTimeProvider.Today;
        var now = dateTimeProvider.UtcNow;

        var slip = await unitOfWork.BorrowSlips.GetQueryable(asNoTracking: true)
            .Include(s => s.Reader)
                .ThenInclude(r => r.ReaderType)
            .Include(s => s.IssuedByStaff)
            .Include(s => s.BorrowDetails.Where(d => d.Status != EntityStatus.Deleted))
                .ThenInclude(d => d.BookCopy)
                    .ThenInclude(c => c.Book)
            .Include(s => s.BorrowDetails.Where(d => d.Status != EntityStatus.Deleted))
                .ThenInclude(d => d.BookCopy)
                    .ThenInclude(c => c.Shelf)
            .Include(s => s.BorrowDetails.Where(d => d.Status != EntityStatus.Deleted))
                .ThenInclude(d => d.ReceivedByStaff)
            .FirstOrDefaultAsync(s => s.Id == id && s.Status != EntityStatus.Deleted, ct);

        if (slip is null) return null;

        var items = slip.BorrowDetails
            .Where(d => d.Status != EntityStatus.Deleted)
            .OrderBy(d => d.DueDate)
            .Select(d =>
            {
                var isOverdue = d.ReturnedAt == null && d.DueDate < today;
                var overdueDays = isOverdue ? Math.Max(0, today.DayNumber - d.DueDate.DayNumber) : 0;
                return new BorrowSlipDetailItemViewModel
                {
                    DetailId = d.Id,
                    BookTitle = d.BookCopy.Book.Title,
                    Barcode = d.BookCopy.Barcode,
                    ShelfLocation = d.BookCopy.Shelf != null ? d.BookCopy.Shelf.Code : null,
                    Price = d.BookCopy.Price,
                    DueDate = d.DueDate,
                    RenewCount = d.RenewCount,
                    ReturnedAt = d.ReturnedAt,
                    ReturnCondition = d.ReturnCondition,
                    ReceivedByStaffName = d.ReceivedByStaff?.FullName,
                    ReturnNote = d.ReturnNote,
                    IsOverdue = isOverdue,
                    OverdueDays = overdueDays
                };
            })
            .ToList();

        var canCancel = slip.BorrowDetails.All(d => d.ReturnedAt == null) && (now - slip.BorrowedAt <= TimeSpan.FromHours(24));

        return new BorrowSlipDetailViewModel
        {
            Id = slip.Id,
            SlipCode = $"PM-{slip.BorrowedAt:yyyyMMdd}-{slip.Id.ToString()[..4].ToUpper()}",
            ReaderId = slip.ReaderId,
            ReaderName = slip.Reader.FullName,
            CardNo = slip.Reader.CardNo,
            ReaderTypeName = slip.Reader.ReaderType.Name,
            ReaderEmail = slip.Reader.Email,
            ReaderPhone = slip.Reader.Phone,
            BorrowedAt = slip.BorrowedAt,
            IssuedByStaffName = slip.IssuedByStaff.FullName,
            Note = slip.Note,
            TotalBooks = items.Count,
            ReturnedBooks = items.Count(i => i.IsReturned),
            CanCancel = canCancel,
            Items = items
        };
    }

    public async Task<ServiceResult> CancelBorrowSlipAsync(Guid id, string? reason, CancellationToken ct = default)
    {
        if (userContext.CurrentUserId is not { } staffId) return ServiceResult.Failure(ErrorCode.AuthUnauthorized);
        if (string.IsNullOrWhiteSpace(reason)) return ServiceResult.ValidationError(ErrorCode.BorrowSlipCancelReasonRequired);

        var slip = await unitOfWork.BorrowSlips.GetQueryable(asNoTracking: false)
            .Include(s => s.BorrowDetails)
                .ThenInclude(d => d.BookCopy)
            .FirstOrDefaultAsync(s => s.Id == id && s.Status != EntityStatus.Deleted, ct);

        if (slip is null) return ServiceResult.Failure(ErrorCode.BorrowSlipNotFound);

        if (slip.BorrowDetails.Any(d => d.ReturnedAt != null))
        {
            return ServiceResult.Failure(ErrorCode.BorrowSlipCannotCancelHasReturned);
        }

        var now = dateTimeProvider.UtcNow;
        if (now - slip.BorrowedAt > TimeSpan.FromHours(24))
        {
            return ServiceResult.Failure(ErrorCode.BorrowSlipCannotCancelExpired);
        }

        var setting = await GetSettingAsync(ct);
        var holdHours = setting?.ReservationHoldHours ?? (short)48;

        // ReleaseCopyAsync saves each copy to advance the reservation queue. Keep the
        // entire cancellation atomic if any later copy or the final slip save fails.
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            foreach (var detail in slip.BorrowDetails.Where(d => d.Status != EntityStatus.Deleted))
            {
                detail.Status = EntityStatus.Deleted;
                detail.ReturnNote = $"Há»§y phiáº¿u: {reason.Trim()}";

                if (detail.BookCopy is { } copy)
                {
                    await ReleaseCopyAsync(copy, holdHours, ct);
                }
            }

            slip.Status = EntityStatus.Deleted;
            slip.Note = string.IsNullOrWhiteSpace(slip.Note)
                ? $"[ÄÃƒ Há»¦Y] {reason.Trim()}"
                : $"{slip.Note} | [ÄÃƒ Há»¦Y] {reason.Trim()}";

            await unitOfWork.CommitTransactionAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
        return ServiceResult.Success("ÄÃ£ há»§y phiáº¿u mÆ°á»£n thÃ nh cÃ´ng. CÃ¡c cuá»‘n sÃ¡ch Ä‘Ã£ Ä‘Æ°á»£c hoÃ n tráº£ láº¡i kho.");
    }

    // ===== Äáº·t trÆ°á»›c =====

    public async Task<ReservationsPageViewModel> GetReservationsPageAsync(ReservationFilter filter, CancellationToken ct = default)
    {
        await ExpireHoldsAsync(await GetSettingAsync(ct), ct);

        var query = unitOfWork.Reservations.GetQueryable(asNoTracking: true).Where(r => r.Status != EntityStatus.Deleted);
        if (!string.IsNullOrWhiteSpace(filter.ReservationStatus))
        {
            query = query.Where(r => r.ReservationStatus == filter.ReservationStatus);
        }
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.ToLower();
            query = query.Where(r => r.Reader.FullName.ToLower().Contains(kw) || r.Reader.CardNo.ToLower().Contains(kw)
                                     || r.Book.Title.ToLower().Contains(kw));
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

        // Chá»‰ Ä‘áº·t trÆ°á»›c Ä‘Æ°á»£c Ä‘áº§u sÃ¡ch háº¿t báº£n sáºµn sÃ ng nhÆ°ng cÃ²n báº£n Ä‘ang mÆ°á»£n / Ä‘ang giá»¯.
        var books = await unitOfWork.Books.GetQueryable(asNoTracking: true)
            .Where(b => b.Status != EntityStatus.Deleted
                        && !b.BookCopies.Any(c => c.Status != EntityStatus.Deleted && c.CopyStatus == CopyStatus.Available)
                        && b.BookCopies.Any(c => c.Status != EntityStatus.Deleted
                                                 && (c.CopyStatus == CopyStatus.Borrowed || c.CopyStatus == CopyStatus.Reserved)))
            .OrderBy(b => b.Title)
            .Select(b => new BookOption(b.Id, b.Title))
            .ToListAsync(ct);

        return new ReservationsPageViewModel { Reservations = paged, Filter = filter, ReservableBooks = books };
    }

    public async Task<ServiceResult> CreateReservationAsync(ReservationCreateViewModel model, CancellationToken ct = default)
    {
        var cardNo = model.CardNo.Trim();

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var reader = await Readers(asNoTracking: true).FirstOrDefaultAsync(r => r.CardNo == cardNo && r.Status == EntityStatus.Active, ct);
            if (reader is null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReaderNotFound);
            }
            if (CardProblem(reader, dateTimeProvider.Today) is { } cardError)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(cardError);
            }

            var book = await unitOfWork.Books.GetQueryable(asNoTracking: true)
                .Where(b => b.Id == model.BookId && b.Status == EntityStatus.Active)
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
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.BookNotFound);
            }
            if (book.HasAvailable)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReservationBookAvailable);
            }
            if (!book.HasCirculating)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReservationNotAvailable);
            }

            if (await OpenLoans(reader.Id).AnyAsync(d => d.BookCopy.BookId == model.BookId, ct))
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReaderAlreadyBorrowingBook);
            }

            var duplicate = await unitOfWork.Reservations.GetQueryable(asNoTracking: true)
                .AnyAsync(r => r.Status != EntityStatus.Deleted && r.ReaderId == reader.Id && r.BookId == model.BookId
                               && (r.ReservationStatus == ReservationStatus.Pending || r.ReservationStatus == ReservationStatus.Ready), ct);
            if (duplicate)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReservationDuplicate);
            }

            await unitOfWork.Reservations.AddAsync(new Reservation
            {
                Id = Guid.NewGuid(),
                ReaderId = reader.Id,
                BookId = model.BookId,
                ReservedAt = dateTimeProvider.UtcNow
            }, ct);
            await unitOfWork.CommitTransactionAsync(ct);

            return ServiceResult.Success($"ÄÃ£ Ä‘áº·t trÆ°á»›c \"{book.Title}\" cho {reader.FullName}.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> CancelReservationAsync(Guid id, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var reservation = await unitOfWork.Reservations.GetQueryable(asNoTracking: false)
                .Include(r => r.AssignedCopy)
                .FirstOrDefaultAsync(r => r.Id == id && r.Status != EntityStatus.Deleted, ct);
            if (reservation is null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReservationNotFound);
            }
            if (reservation.ReservationStatus is not (ReservationStatus.Pending or ReservationStatus.Ready))
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReservationAlreadyClosed);
            }

            var wasReady = reservation.ReservationStatus == ReservationStatus.Ready;
            reservation.ReservationStatus = ReservationStatus.Cancelled;
            reservation.ClosedAt = dateTimeProvider.UtcNow;

            var setting = await GetSettingAsync(ct);
            if (wasReady && setting is not null && reservation.AssignedCopy is { CopyStatus: CopyStatus.Reserved } copy)
            {
                await ReleaseCopyAsync(copy, setting.ReservationHoldHours, ct);
            }

            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success("ÄÃ£ há»§y phiáº¿u Ä‘áº·t trÆ°á»›c.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // ===== Thu pháº¡t =====

    public async Task<FinesPageViewModel> GetFinesPageAsync(FineFilter filter, CancellationToken ct = default)
    {
        var query = unitOfWork.Fines.GetQueryable(asNoTracking: true).Where(f => f.Status != EntityStatus.Deleted);
        if (!string.IsNullOrWhiteSpace(filter.FineStatus))
        {
            query = query.Where(f => f.FineStatus == filter.FineStatus);
        }
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.ToLower();
            query = query.Where(f => f.BorrowDetail.BorrowSlip.Reader.FullName.ToLower().Contains(kw)
                                     || f.BorrowDetail.BorrowSlip.Reader.CardNo.ToLower().Contains(kw));
        }

        var paged = await query
            .OrderBy(f => f.FineStatus == FineStatus.Unpaid ? 0 : 1)
            .ThenByDescending(f => f.CreatedAt)
            .Select(f => new FineItemViewModel
            {
                Id = f.Id,
                ReaderName = f.BorrowDetail.BorrowSlip.Reader.FullName,
                CardNo = f.BorrowDetail.BorrowSlip.Reader.CardNo,
                BookTitle = f.BorrowDetail.BookCopy.Book.Title,
                Barcode = f.BorrowDetail.BookCopy.Barcode,
                FineType = f.FineType,
                OverdueDays = f.OverdueDays,
                Amount = f.Amount,
                FineStatus = f.FineStatus,
                CreatedAt = f.CreatedAt,
                PaidAt = f.PaidAt,
                PaymentMethod = f.PaymentMethod,
                Note = f.Note
            })
            .ToPagedResultAsync(filter, ct);

        var unpaidTotal = await unitOfWork.Fines.GetQueryable(asNoTracking: true)
            .Where(f => f.Status != EntityStatus.Deleted && f.FineStatus == FineStatus.Unpaid)
            .SumAsync(f => f.Amount, ct);

        return new FinesPageViewModel { Fines = paged, Filter = filter, UnpaidTotal = unpaidTotal };
    }

    public async Task<ServiceResult> PayFineAsync(Guid id, string? paymentMethod, CancellationToken ct = default)
    {
        if (paymentMethod is not (PaymentMethod.Cash or PaymentMethod.BankTransfer))
        {
            return ServiceResult.ValidationError(ErrorCode.CommonInvalidRequest);
        }

        var fine = await FindUnpaidFineAsync(id, ct);
        if (fine.Error is { } error) return ServiceResult.Failure(error);

        fine.Fine!.FineStatus = FineStatus.Paid;
        fine.Fine.PaymentMethod = paymentMethod;
        fine.Fine.PaidAt = dateTimeProvider.UtcNow;
        fine.Fine.HandledBy = userContext.CurrentUserId;
        await unitOfWork.SaveChangesAsync(ct);

        return ServiceResult.Success($"ÄÃ£ thu {CurrencyHelper.ToVnd(fine.Fine.Amount)}.");
    }

    public async Task<ServiceResult> WaiveFineAsync(Guid id, string? note, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(note)) return ServiceResult.ValidationError(ErrorCode.FineWaiveNoteRequired);

        var fine = await FindUnpaidFineAsync(id, ct);
        if (fine.Error is { } error) return ServiceResult.Failure(error);

        fine.Fine!.FineStatus = FineStatus.Waived;
        fine.Fine.Note = note.Trim();
        fine.Fine.HandledBy = userContext.CurrentUserId;
        await unitOfWork.SaveChangesAsync(ct);

        return ServiceResult.Success($"ÄÃ£ miá»…n khoáº£n pháº¡t {CurrencyHelper.ToVnd(fine.Fine.Amount)}.");
    }

    // ===== DÃ¹ng chung =====

    private IQueryable<Reader> Readers(bool asNoTracking) =>
        unitOfWork.Readers.GetQueryable(asNoTracking)
            .Include(r => r.ReaderType)
            .Where(r => r.Status != EntityStatus.Deleted);

    private IQueryable<BorrowDetail> OpenLoans(Guid readerId) =>
        unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .Where(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null
                        && d.BorrowSlip.ReaderId == readerId && d.BorrowSlip.Status != EntityStatus.Deleted);

    private Task<decimal> UnpaidFinesAsync(Guid readerId, CancellationToken ct) =>
        unitOfWork.Fines.GetQueryable(asNoTracking: true)
            .Where(f => f.Status != EntityStatus.Deleted && f.FineStatus == FineStatus.Unpaid
                        && f.BorrowDetail.BorrowSlip.ReaderId == readerId)
            .SumAsync(f => f.Amount, ct);

    private Task<LibrarySetting?> GetSettingAsync(CancellationToken ct) =>
        unitOfWork.LibrarySettings.GetQueryable(asNoTracking: true)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(ct);

    private static ErrorCode? CardProblem(Reader reader, DateOnly today) =>
        reader.CardStatus == CardStatus.Locked ? ErrorCode.ReaderCardLocked
        : reader.CardStatus == CardStatus.Expired || reader.CardExpiryDate < today ? ErrorCode.ReaderCardExpired
        : null;

    /// <summary>
    /// CÃ¡c lÃ½ do cháº·n mÆ°á»£n theo thá»© tá»± kiá»ƒm tra: tháº», sÃ¡ch quÃ¡ háº¡n, ná»£ pháº¡t, háº¡n má»©c.
    /// </summary>
    private async Task<(List<string> Reasons, int Borrowed, decimal Unpaid)> CheckBorrowerAsync(Reader reader, CancellationToken ct)
    {
        var today = dateTimeProvider.Today;
        var reasons = new List<string>();
        if (CardProblem(reader, today) is { } cardError) reasons.Add(ErrorMessages.Get(cardError));

        var dueDates = await OpenLoans(reader.Id).Select(d => d.DueDate).ToListAsync(ct);
        if (dueDates.Any(d => d < today)) reasons.Add(ErrorMessages.Get(ErrorCode.ReaderHasOverdueBooks));

        var unpaid = await UnpaidFinesAsync(reader.Id, ct);
        if (unpaid > 0) reasons.Add(ErrorMessages.Format(ErrorCode.ReaderHasUnpaidFines, CurrencyHelper.ToVnd(unpaid)));

        if (dueDates.Count >= reader.ReaderType.MaxBorrowBooks)
        {
            reasons.Add(ErrorMessages.Format(ErrorCode.ReaderMaxBorrowLimitExceeded, reader.ReaderType.MaxBorrowBooks));
        }

        return (reasons, dueDates.Count, unpaid);
    }

    private static ServiceResult CopyFailure(ErrorCode code, string barcode) =>
        ServiceResult.Failure(code, $"{barcode}: {ErrorMessages.Get(code)}");

    private static ServiceResult<T> CopyFailure<T>(ErrorCode code, string barcode) =>
        ServiceResult<T>.Failure(code, $"{barcode}: {ErrorMessages.Get(code)}");

    private async Task<decimal> AddFineAsync(BorrowDetail detail, string fineType, decimal amount, int? overdueDays, CancellationToken ct)
    {
        if (amount <= 0) return 0;
        await unitOfWork.Fines.AddAsync(new Fine
        {
            Id = Guid.NewGuid(),
            BorrowDetailId = detail.Id,
            FineType = fineType,
            OverdueDays = overdueDays,
            Amount = amount
        }, ct);
        return amount;
    }

    private async Task<(Fine? Fine, ErrorCode? Error)> FindUnpaidFineAsync(Guid id, CancellationToken ct)
    {
        var fine = await unitOfWork.Fines.FirstOrDefaultAsync(f => f.Id == id && f.Status != EntityStatus.Deleted, asNoTracking: false, ct);
        if (fine is null) return (null, ErrorCode.FineNotFound);
        return fine.FineStatus == FineStatus.Unpaid ? (fine, null) : (null, ErrorCode.FineAlreadySettled);
    }

    /// <summary>
    /// Báº£n sao quay vá» kho: giá»¯ cho ngÆ°á»i Ä‘áº·t trÆ°á»›c sá»›m nháº¥t cá»§a Ä‘áº§u sÃ¡ch, khÃ´ng cÃ³ ai thÃ¬ sáºµn sÃ ng cho mÆ°á»£n.
    /// </summary>
    private async Task ReleaseCopyAsync(BookCopy copy, short holdHours, CancellationToken ct)
    {
        var now = dateTimeProvider.UtcNow;
        var next = await unitOfWork.Reservations.GetQueryable(asNoTracking: false)
            .Where(r => r.Status != EntityStatus.Deleted &&
                        r.BookId == copy.BookId &&
                        r.Reader.Status == EntityStatus.Active &&
                        r.Book.Status == EntityStatus.Active &&
                        r.ReservationStatus == ReservationStatus.Pending)
            .OrderBy(r => r.ReservedAt)
            .FirstOrDefaultAsync(ct);

        if (next is null)
        {
            copy.CopyStatus = CopyStatus.Available;
        }
        else
        {
            next.ReservationStatus = ReservationStatus.Ready;
            next.AssignedCopyId = copy.Id;
            next.ReadyAt = now;
            next.ExpiresAt = now.AddHours(holdHours);
            copy.CopyStatus = CopyStatus.Reserved;
        }

        // LÆ°u ngay Ä‘á»ƒ láº§n chuyá»ƒn káº¿ tiáº¿p cá»§a cÃ¹ng Ä‘áº§u sÃ¡ch khÃ´ng chá»n láº¡i cÃ¹ng má»™t ngÆ°á»i Ä‘áº·t.
        await unitOfWork.SaveChangesAsync(ct);
    }

    // ponytail: háº¿t háº¡n giá»¯ sÃ¡ch xá»­ lÃ½ lazy khi má»Ÿ quáº§y mÆ°á»£n / mÃ n Ä‘áº·t trÆ°á»›c; cáº§n Ä‘Ãºng giá» thÃ¬ chuyá»ƒn sang IHostedService.
    private async Task ExpireHoldsAsync(LibrarySetting? setting, CancellationToken ct)
    {
        if (setting is null) return;

        var now = dateTimeProvider.UtcNow;
        var expired = await unitOfWork.Reservations.GetQueryable(asNoTracking: false)
            .Include(r => r.AssignedCopy)
            .Where(r => r.Status != EntityStatus.Deleted && r.ReservationStatus == ReservationStatus.Ready && r.ExpiresAt < now)
            .ToListAsync(ct);

        foreach (var reservation in expired)
        {
            reservation.ReservationStatus = ReservationStatus.Expired;
            reservation.ClosedAt = now;
            if (reservation.AssignedCopy is { CopyStatus: CopyStatus.Reserved } copy)
            {
                await ReleaseCopyAsync(copy, setting.ReservationHoldHours, ct);
            }
            else
            {
                await unitOfWork.SaveChangesAsync(ct);
            }
        }
    }
}
