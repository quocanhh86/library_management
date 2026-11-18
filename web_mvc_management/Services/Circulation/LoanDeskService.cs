using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Helper;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Circulation;
using web_mvc_management.Repositories;
using static web_mvc_management.Constant.LibraryConstants;

namespace web_mvc_management.Services;

public class LoanDeskService(
    IUnitOfWork unitOfWork,
    ICirculationPolicy circulationPolicy,
    IReservationQueueEngine reservationQueueEngine,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ILoanDeskService
{
    public async Task<CirculationDeskViewModel> GetDeskAsync(bool returning, string? card, CancellationToken ct = default)
    {
        var today = dateTimeProvider.Today;
        var setting = await circulationPolicy.FindSettingAsync(ct);
        if (!returning)
        {
            await reservationQueueEngine.ExpireOverdueHoldsAsync(ct);
        }

        var cardNo = card?.Trim();
        var reader = string.IsNullOrEmpty(cardNo)
            ? null
            : await ReadersQuery(asNoTracking: true).FirstOrDefaultAsync(r => r.CardNo == cardNo, ct);

        var finePerDay = setting?.OverdueFinePerDay ?? 0m;
        if (reader is null)
        {
            return new CirculationDeskViewModel
            {
                Returning = returning,
                Today = today,
                Card = cardNo,
                FinePerDay = finePerDay,
                Error = string.IsNullOrEmpty(cardNo) ? null : ErrorMessages.Get(ErrorCode.ReaderNotFound)
            };
        }

        var eligibility = await circulationPolicy.EvaluateReaderAsync(reader, ct);
        var loans = await OpenLoansQuery(reader.Id)
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
            .Select(r => new DeskHold(r.Book.Title, r.AssignedCopy != null ? r.AssignedCopy.Barcode : string.Empty, r.ExpiresAt))
            .ToListAsync(ct);

        var cardStatus = circulationPolicy.GetCardBlockingError(reader, today) == ErrorCode.ReaderCardExpired
            ? CardStatus.Expired
            : reader.CardStatus;

        return new CirculationDeskViewModel
        {
            Returning = returning,
            Today = today,
            Card = cardNo,
            FinePerDay = finePerDay,
            LoanDays = reader.ReaderType.LoanDays,
            MaxRenewals = reader.ReaderType.MaxRenewals,
            Reader = new DeskReader(
                reader.Id,
                reader.FullName,
                reader.CardNo,
                reader.ReaderType.Name,
                reader.CardExpiryDate,
                cardStatus,
                eligibility.ActiveLoanCount,
                reader.ReaderType.MaxBorrowBooks,
                eligibility.UnpaidFines),
            BlockReasons = [.. eligibility.BlockReasons],
            Holds = holds,
            Loans = loans
                .Select(l => new DeskLoan(
                    l.Id,
                    l.Title,
                    l.Barcode,
                    l.BorrowedAt,
                    l.DueDate,
                    l.RenewCount,
                    Math.Max(0, today.DayNumber - l.DueDate.DayNumber - l.ChargedDays) * finePerDay))
                .ToList()
        };
    }

    public Task<ServiceResult<Guid>> CreateBorrowSlipAsync(BorrowSubmitViewModel model, CancellationToken ct = default)
    {
        if (userContext.CurrentUserId is not { } staffId)
        {
            return Task.FromResult(ServiceResult<Guid>.Failure(ErrorCode.AuthUnauthorized));
        }

        var barcodes = model.Barcodes.Select(b => b?.Trim() ?? string.Empty).Where(b => b.Length > 0).ToList();
        if (barcodes.Count == 0)
        {
            return Task.FromResult(ServiceResult<Guid>.ValidationError(ErrorCode.BorrowListEmpty));
        }

        var duplicate = barcodes.GroupBy(b => b, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            return Task.FromResult(ServiceResult<Guid>.ValidationError(
                ErrorCode.BorrowDuplicateBarcode,
                ErrorMessages.Format(ErrorCode.BorrowDuplicateBarcode, duplicate.Key)));
        }

        return unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            await reservationQueueEngine.ExpireOverdueHoldsAsync(ct);

            var reader = await ReadersQuery(asNoTracking: true)
                .FirstOrDefaultAsync(r => r.Id == model.ReaderId && r.Status == EntityStatus.Active, ct);
            if (reader is null)
            {
                return ServiceResult<Guid>.Failure(ErrorCode.ReaderNotFound);
            }

            var eligibility = await circulationPolicy.EvaluateReaderAsync(reader, ct);
            if (!eligibility.CanBorrow)
            {
                return ServiceResult<Guid>.Failure(
                    eligibility.FirstErrorCode ?? ErrorCode.CommonValidationError,
                    eligibility.BlockReasons[0]);
            }

            var limit = reader.ReaderType.MaxBorrowBooks;
            if (eligibility.ActiveLoanCount + barcodes.Count > limit)
            {
                return ServiceResult<Guid>.Failure(
                    ErrorCode.ReaderMaxBorrowLimitExceeded,
                    ErrorMessages.Format(ErrorCode.ReaderMaxBorrowLimitExceeded, limit));
            }

            var pickResult = await ValidateAndPreparePickedCopiesAsync(reader.Id, barcodes, ct);
            if (!pickResult.IsSuccess)
            {
                return ServiceResult<Guid>.Failure(pickResult.Code ?? ErrorCode.BookCopyNotAvailable, pickResult.Message);
            }

            var (picked, copiesToRelease) = pickResult.Data;
            var now = dateTimeProvider.UtcNow;
            var dueDate = dateTimeProvider.Today.AddDays(reader.ReaderType.LoanDays);

            var slip = new BorrowSlip
            {
                Id = Guid.NewGuid(),
                ReaderId = reader.Id,
                IssuedBy = staffId,
                BorrowedAt = now,
                Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim()
            };
            await unitOfWork.BorrowSlips.AddAsync(slip, ct);

            // Lưu ý hợp đồng trigger SQL trg_borrow_details_sync_copy:
            // Không gán CopyStatus = Borrowed trước khi INSERT BorrowDetail; trigger sẽ tự đồng bộ sang BORROWED.
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

            foreach (var releasedCopy in copiesToRelease)
            {
                await reservationQueueEngine.AssignCopyToNextPendingOrReleaseAsync(releasedCopy, ct: ct);
            }

            return ServiceResult<Guid>.Success(
                slip.Id,
                $"Đã lập phiếu mượn {picked.Count} cuốn cho {reader.FullName}. Hạn trả {dueDate.ToString(AppConstants.DefaultDateFormat)}.");
        }, cancellationToken: ct);
    }

    public Task<ServiceResult> ReturnAsync(ReturnSubmitViewModel model, CancellationToken ct = default)
    {
        if (userContext.CurrentUserId is not { } staffId)
        {
            return Task.FromResult(ServiceResult.Failure(ErrorCode.AuthUnauthorized));
        }

        var items = model.Items.Where(i => i.Selected).DistinctBy(i => i.DetailId).ToList();
        if (items.Count == 0)
        {
            return Task.FromResult(ServiceResult.ValidationError(ErrorCode.CommonInvalidRequest));
        }

        if (items.Any(i => i.Condition is not (ReturnCondition.Good or ReturnCondition.Damaged or ReturnCondition.Lost)))
        {
            return Task.FromResult(ServiceResult.ValidationError(ErrorCode.ReturnConditionInvalid));
        }

        return unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var setting = await circulationPolicy.FindSettingAsync(ct);
            if (setting is null)
            {
                return ServiceResult.Failure(ErrorCode.LibrarySettingNotFound);
            }

            var ids = items.Select(i => i.DetailId).ToList();
            var details = await unitOfWork.BorrowDetails.GetQueryable(asNoTracking: false)
                .Include(d => d.BookCopy)
                .Include(d => d.BorrowSlip)
                .Include(d => d.Fines)
                .Where(d => ids.Contains(d.Id) && d.Status != EntityStatus.Deleted && d.BorrowSlip.Status != EntityStatus.Deleted)
                .ToListAsync(ct);

            if (details.Count != ids.Count || details.Any(d => d.BorrowSlip.ReaderId != model.ReaderId))
            {
                return ServiceResult.Failure(ErrorCode.BorrowDetailNotFound);
            }

            if (details.Any(d => d.ReturnedAt != null))
            {
                return ServiceResult.Failure(ErrorCode.BookAlreadyReturned);
            }

            var now = dateTimeProvider.UtcNow;
            var today = dateTimeProvider.Today;
            var totalFine = 0m;

            foreach (var item in items)
            {
                var detail = details.First(d => d.Id == item.DetailId);
                detail.ReturnedAt = now;
                detail.ReceivedBy = staffId;
                detail.ReturnCondition = item.Condition;
                detail.ReturnNote = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim();

                totalFine += await ApplyReturnFinesAsync(detail, item.Condition, setting, today, ct);

                if (item.Condition == ReturnCondition.Good)
                {
                    // Hợp đồng trigger trg_borrow_details_sync_copy:
                    // SaveChanges trước khi gọi ReservationQueueEngine để trigger chạy xong trước khi chuyển bản sao sang RESERVED.
                    await unitOfWork.SaveChangesAsync(ct);
                    await reservationQueueEngine.AssignCopyToNextPendingOrReleaseAsync(detail.BookCopy, ct: ct);
                }
                else
                {
                    var damaged = item.Condition == ReturnCondition.Damaged;
                    detail.BookCopy.CopyStatus = damaged ? CopyStatus.Damaged : CopyStatus.Lost;
                }
            }

            var message = $"Đã nhận trả {items.Count} cuốn.";
            if (totalFine > 0)
            {
                message += $" Phát sinh phạt {CurrencyHelper.ToVnd(totalFine)}, thu tại màn Thu phạt.";
            }
            return ServiceResult.Success(message);
        }, cancellationToken: ct);
    }

    public Task<ServiceResult> RenewAsync(Guid detailId, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var detail = await unitOfWork.BorrowDetails.GetQueryable(asNoTracking: false)
                .Include(d => d.BookCopy).ThenInclude(c => c.Book)
                .Include(d => d.BorrowSlip).ThenInclude(s => s.Reader).ThenInclude(r => r.ReaderType)
                .FirstOrDefaultAsync(d => d.Id == detailId && d.Status != EntityStatus.Deleted && d.BorrowSlip.Status != EntityStatus.Deleted, ct);

            if (detail is null)
            {
                return ServiceResult.Failure(ErrorCode.BorrowDetailNotFound);
            }

            var policyCheck = await circulationPolicy.ValidateRenewAsync(detail, ct);
            if (!policyCheck.IsSuccess)
            {
                return policyCheck;
            }

            var type = detail.BorrowSlip.Reader.ReaderType;
            detail.DueDate = detail.DueDate.AddDays(type.RenewDays);
            detail.RenewCount++;

            return ServiceResult.Success(
                $"Đã gia hạn đến {detail.DueDate.ToString(AppConstants.DefaultDateFormat)}. Còn {type.MaxRenewals - detail.RenewCount} lần gia hạn.");
        }, cancellationToken: ct);

    public async Task<BorrowHistoryPageViewModel> GetBorrowHistoryPageAsync(
        BorrowSlipFilter filter,
        CancellationToken ct = default)
    {
        var today = dateTimeProvider.Today;
        var baseQuery = unitOfWork.BorrowSlips.GetQueryable(asNoTracking: true)
            .Where(s => s.Status != EntityStatus.Deleted);

        var totalActive = await baseQuery.CountAsync(
            s => s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null), ct);
        var totalOverdue = await baseQuery.CountAsync(
            s => s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null && d.DueDate < today), ct);
        var totalReturned = await baseQuery.CountAsync(s =>
            s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted) &&
            !s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null), ct);

        var query = baseQuery;

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim();
            var guidPrefixLower = SlipCodeHelper.ExtractSearchGuidPrefix(kw).ToLowerInvariant();
            query = query.Where(s =>
                s.Reader.FullName.Contains(kw) ||
                s.Reader.CardNo.Contains(kw) ||
                s.Id.ToString().ToLower().StartsWith(guidPrefixLower) ||
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
            query = filter.BorrowStatus.ToLowerInvariant() switch
            {
                "borrowing" => query.Where(s => s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null)),
                "overdue" => query.Where(s => s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null && d.DueDate < today)),
                "returned" => query.Where(s =>
                    s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted) &&
                    !s.BorrowDetails.Any(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null)),
                _ => query
            };
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
            var status = (totalBooks > 0 && returnedBooks == totalBooks)
                ? "Đã trả đủ"
                : (hasOverdue ? "Có sách quá hạn" : "Đang mượn");

            return new BorrowSlipItemViewModel
            {
                Id = s.Id,
                SlipCode = SlipCodeHelper.Format(s.Id, s.BorrowedAt),
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
                BookTitles = [.. s.Details.Select(d => d.BookTitle)]
            };
        }).ToList();

        return new BorrowHistoryPageViewModel
        {
            Slips = new PagedResult<BorrowSlipItemViewModel>(list, totalCount, normalizedPageNo, safePageItemSize),
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
            .Include(s => s.Reader).ThenInclude(r => r.ReaderType)
            .Include(s => s.IssuedByStaff)
            .Include(s => s.BorrowDetails.Where(d => d.Status != EntityStatus.Deleted))
                .ThenInclude(d => d.BookCopy).ThenInclude(c => c.Book)
            .Include(s => s.BorrowDetails.Where(d => d.Status != EntityStatus.Deleted))
                .ThenInclude(d => d.BookCopy).ThenInclude(c => c.Shelf)
            .Include(s => s.BorrowDetails.Where(d => d.Status != EntityStatus.Deleted))
                .ThenInclude(d => d.ReceivedByStaff)
            .FirstOrDefaultAsync(s => s.Id == id && s.Status != EntityStatus.Deleted, ct);

        if (slip is null)
        {
            return null;
        }

        var items = slip.BorrowDetails
            .Where(d => d.Status != EntityStatus.Deleted)
            .OrderBy(d => d.DueDate)
            .Select(d =>
            {
                var isOverdue = d.ReturnedAt == null && d.DueDate < today;
                return new BorrowSlipDetailItemViewModel
                {
                    DetailId = d.Id,
                    BookTitle = d.BookCopy.Book.Title,
                    Barcode = d.BookCopy.Barcode,
                    ShelfLocation = d.BookCopy.Shelf?.Code,
                    Price = d.BookCopy.Price,
                    DueDate = d.DueDate,
                    RenewCount = d.RenewCount,
                    ReturnedAt = d.ReturnedAt,
                    ReturnCondition = d.ReturnCondition,
                    ReceivedByStaffName = d.ReceivedByStaff?.FullName,
                    ReturnNote = d.ReturnNote,
                    IsOverdue = isOverdue,
                    OverdueDays = isOverdue ? Math.Max(0, today.DayNumber - d.DueDate.DayNumber) : 0
                };
            })
            .ToList();

        var canCancel = slip.BorrowDetails.All(d => d.ReturnedAt == null)
                        && (now - slip.BorrowedAt <= TimeSpan.FromHours(Defaults.BorrowCancelWindowHours));

        return new BorrowSlipDetailViewModel
        {
            Id = slip.Id,
            SlipCode = SlipCodeHelper.Format(slip.Id, slip.BorrowedAt),
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

    public Task<ServiceResult> CancelBorrowSlipAsync(Guid id, string? reason, CancellationToken ct = default)
    {
        if (userContext.CurrentUserId is not { } staffId)
        {
            return Task.FromResult(ServiceResult.Failure(ErrorCode.AuthUnauthorized));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Task.FromResult(ServiceResult.ValidationError(ErrorCode.BorrowSlipCancelReasonRequired));
        }

        return unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var slip = await unitOfWork.BorrowSlips.GetQueryable(asNoTracking: false)
                .Include(s => s.BorrowDetails)
                    .ThenInclude(d => d.BookCopy)
                .FirstOrDefaultAsync(s => s.Id == id && s.Status != EntityStatus.Deleted, ct);

            if (slip is null)
            {
                return ServiceResult.Failure(ErrorCode.BorrowSlipNotFound);
            }

            if (slip.BorrowDetails.Any(d => d.ReturnedAt != null))
            {
                return ServiceResult.Failure(ErrorCode.BorrowSlipCannotCancelHasReturned);
            }

            var now = dateTimeProvider.UtcNow;
            if (now - slip.BorrowedAt > TimeSpan.FromHours(Defaults.BorrowCancelWindowHours))
            {
                return ServiceResult.Failure(ErrorCode.BorrowSlipCannotCancelExpired);
            }

            var reasonTrim = reason.Trim();
            foreach (var detail in slip.BorrowDetails.Where(d => d.Status != EntityStatus.Deleted))
            {
                detail.Status = EntityStatus.Deleted;
                detail.UpdatedAt = now;
                detail.UpdatedBy = staffId;
                detail.ReturnNote = $"Hủy phiếu: {reasonTrim}";

                if (detail.BookCopy is { } copy)
                {
                    await reservationQueueEngine.AssignCopyToNextPendingOrReleaseAsync(copy, ct: ct);
                }
            }

            slip.Status = EntityStatus.Deleted;
            slip.UpdatedAt = now;
            slip.UpdatedBy = staffId;
            slip.Note = string.IsNullOrWhiteSpace(slip.Note)
                ? $"[ĐÃ HỦY] {reasonTrim}"
                : $"{slip.Note} | [ĐÃ HỦY] {reasonTrim}";

            return ServiceResult.Success("Đã hủy phiếu mượn thành công. Các cuốn sách đã được hoàn trả lại kho.");
        }, cancellationToken: ct);
    }

    // ===== Helpers nội bộ =====

    private IQueryable<Reader> ReadersQuery(bool asNoTracking) =>
        unitOfWork.Readers.GetQueryable(asNoTracking)
            .Include(r => r.ReaderType)
            .Where(r => r.Status != EntityStatus.Deleted);

    private IQueryable<BorrowDetail> OpenLoansQuery(Guid readerId) =>
        unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .Where(d => d.Status != EntityStatus.Deleted
                        && d.ReturnedAt == null
                        && d.BorrowSlip.ReaderId == readerId
                        && d.BorrowSlip.Status != EntityStatus.Deleted);

    private async Task<ServiceResult<(List<BookCopy> Picked, List<BookCopy> CopiesToRelease)>> ValidateAndPreparePickedCopiesAsync(
        Guid readerId,
        List<string> barcodes,
        CancellationToken ct)
    {
        var openBookIds = await OpenLoansQuery(readerId).Select(d => d.BookCopy.BookId).ToListAsync(ct);

        var copies = await unitOfWork.BookCopies.GetQueryable(asNoTracking: false)
            .Include(c => c.Book)
            .Where(c => c.Status == EntityStatus.Active && c.Book.Status == EntityStatus.Active && barcodes.Contains(c.Barcode))
            .ToListAsync(ct);

        var copyIds = copies.Select(c => c.Id).ToList();
        var holds = await unitOfWork.Reservations.GetQueryable(asNoTracking: false)
            .Where(r => r.Status != EntityStatus.Deleted
                        && r.ReservationStatus == ReservationStatus.Ready
                        && r.AssignedCopyId != null
                        && copyIds.Contains(r.AssignedCopyId.Value))
            .ToListAsync(ct);

        var readerOpenReservations = await unitOfWork.Reservations.GetQueryable(asNoTracking: false)
            .Include(r => r.AssignedCopy)
            .Where(r => r.Status != EntityStatus.Deleted
                        && r.ReaderId == readerId
                        && (r.ReservationStatus == ReservationStatus.Pending || r.ReservationStatus == ReservationStatus.Ready))
            .ToListAsync(ct);

        var now = dateTimeProvider.UtcNow;
        var picked = new List<BookCopy>();
        var copiesToRelease = new List<BookCopy>();

        foreach (var code in barcodes)
        {
            var copy = copies.FirstOrDefault(c => string.Equals(c.Barcode, code, StringComparison.OrdinalIgnoreCase));
            if (copy is null)
            {
                return CopyFailure<(List<BookCopy>, List<BookCopy>)>(ErrorCode.BookCopyNotFound, code);
            }

            if (openBookIds.Contains(copy.BookId) || picked.Any(p => p.BookId == copy.BookId))
            {
                return ServiceResult<(List<BookCopy>, List<BookCopy>)>.Failure(
                    ErrorCode.ReaderAlreadyBorrowingBook,
                    $"{copy.Barcode}: {ErrorMessages.Get(ErrorCode.ReaderAlreadyBorrowingBook)} ({copy.Book.Title})");
            }

            switch (copy.CopyStatus)
            {
                case CopyStatus.Available:
                    var readerBookRes = readerOpenReservations.FirstOrDefault(r => r.BookId == copy.BookId);
                    if (readerBookRes is not null)
                    {
                        var prevStatus = readerBookRes.ReservationStatus;
                        var prevAssignedCopy = readerBookRes.AssignedCopy;
                        readerBookRes.ReservationStatus = ReservationStatus.Fulfilled;
                        readerBookRes.ClosedAt = now;
                        if (prevStatus == ReservationStatus.Ready &&
                            prevAssignedCopy is { CopyStatus: CopyStatus.Reserved } &&
                            prevAssignedCopy.Id != copy.Id)
                        {
                            copiesToRelease.Add(prevAssignedCopy);
                        }
                    }
                    break;
                case CopyStatus.Reserved:
                    var hold = holds.FirstOrDefault(h => h.AssignedCopyId == copy.Id);
                    if (hold is null || hold.ReaderId != readerId)
                    {
                        return ServiceResult<(List<BookCopy>, List<BookCopy>)>.Failure(
                            ErrorCode.BookCopyReservedForOther,
                            ErrorMessages.Format(ErrorCode.BookCopyReservedForOther, copy.Barcode));
                    }
                    hold.ReservationStatus = ReservationStatus.Fulfilled;
                    hold.ClosedAt = now;
                    break;
                case CopyStatus.Damaged or CopyStatus.Lost:
                    return CopyFailure<(List<BookCopy>, List<BookCopy>)>(ErrorCode.BookCopyDamagedOrLost, copy.Barcode);
                default:
                    return CopyFailure<(List<BookCopy>, List<BookCopy>)>(ErrorCode.BookCopyNotAvailable, copy.Barcode);
            }

            picked.Add(copy);
        }

        return ServiceResult<(List<BookCopy>, List<BookCopy>)>.Success((picked, copiesToRelease));
    }

    private async Task<decimal> ApplyReturnFinesAsync(
        BorrowDetail detail,
        string returnCondition,
        LibrarySetting setting,
        DateOnly today,
        CancellationToken ct)
    {
        var generatedFine = 0m;

        var chargedDays = detail.Fines
            .Where(f => f.Status != EntityStatus.Deleted && f.FineType == FineType.Overdue && f.FineStatus != FineStatus.Cancelled)
            .Sum(f => f.OverdueDays ?? 0);
        var lateDays = today.DayNumber - detail.DueDate.DayNumber - chargedDays;
        if (lateDays > 0)
        {
            generatedFine += await UpsertFineAsync(detail, FineType.Overdue, lateDays * setting.OverdueFinePerDay, lateDays, ct);
        }

        if (returnCondition == ReturnCondition.Good)
        {
            return generatedFine;
        }

        var damaged = returnCondition == ReturnCondition.Damaged;
        if (detail.BookCopy.Price is { } price)
        {
            var percent = damaged ? setting.DamagedFinePercent : setting.LostFinePercent;
            generatedFine += await UpsertFineAsync(
                detail,
                damaged ? FineType.Damaged : FineType.Lost,
                Math.Round(price * percent / 100m, 0),
                null,
                ct);
        }
        else
        {
            detail.ReturnNote = string.Join(" ", detail.ReturnNote, "Ghi chú: Bản sao chưa có giá bìa nên chưa tính phạt bồi thường.").Trim();
        }

        return generatedFine;
    }

    private async Task<decimal> UpsertFineAsync(
        BorrowDetail detail,
        string fineType,
        decimal amount,
        int? overdueDays,
        CancellationToken ct)
    {
        if (amount <= 0)
        {
            return 0;
        }

        // Tránh vi phạm uq_fines_detail_type (borrow_detail_id, fine_type) nếu dòng mượn đã có bản ghi cùng loại phạt
        var existingFine = detail.Fines.FirstOrDefault(f => f.FineType == fineType);
        if (existingFine is not null)
        {
            if (existingFine.Status == EntityStatus.Deleted || existingFine.FineStatus != FineStatus.Unpaid)
            {
                existingFine.Status = EntityStatus.Active;
                existingFine.FineStatus = FineStatus.Unpaid;
                existingFine.PaidAt = null;
                existingFine.PaymentMethod = null;
                existingFine.HandledBy = null;
                existingFine.Amount = amount;
                existingFine.OverdueDays = fineType == FineType.Overdue ? overdueDays : null;
            }
            else
            {
                existingFine.Amount += amount;
                if (fineType == FineType.Overdue && overdueDays.HasValue)
                {
                    existingFine.OverdueDays = (existingFine.OverdueDays ?? 0) + overdueDays.Value;
                }
            }
            return amount;
        }

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

    private static ServiceResult<T> CopyFailure<T>(ErrorCode code, string barcode) =>
        ServiceResult<T>.Failure(code, $"{barcode}: {ErrorMessages.Get(code)}");
}
