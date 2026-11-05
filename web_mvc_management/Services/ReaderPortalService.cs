using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Helper;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Home;
using web_mvc_management.Models.ViewModels.Reader;
using web_mvc_management.Models.ViewModels.Shared;
using web_mvc_management.Repositories;
using static web_mvc_management.Constant.LibraryConstants;

namespace web_mvc_management.Services;

/// <summary>
/// Triển khai các nghiệp vụ Cổng Bạn Đọc Tự Phục Vụ kết nối trực tiếp SQL Server qua UnitOfWork.
/// </summary>
public class ReaderPortalService(
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider) : IReaderPortalService
{
    public async Task<ReaderLoansViewModel?> GetHomeLoansAsync(Guid readerId, CancellationToken ct = default)
    {
        var readerExists = await unitOfWork.Readers.ExistsAsync(
            r => r.Id == readerId && r.Status != EntityStatus.Deleted, ct);
        if (!readerExists)
        {
            return null;
        }

        var loans = await unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .Where(d => d.Status != EntityStatus.Deleted
                        && d.ReturnedAt == null
                        && d.BorrowSlip.ReaderId == readerId
                        && d.BorrowSlip.Status != EntityStatus.Deleted)
            .OrderBy(d => d.DueDate)
            .Select(d => new
            {
                d.BookCopy.Book.Title,
                Authors = d.BookCopy.Book.BookAuthors
                    .Where(ba => ba.Status != EntityStatus.Deleted)
                    .OrderBy(ba => ba.AuthorOrder)
                    .Select(ba => ba.Author.FullName)
                    .ToList(),
                d.DueDate
            })
            .ToListAsync(ct);

        var unpaidFines = await GetTotalUnpaidFineAsync(readerId, ct);

        return new ReaderLoansViewModel
        {
            Loans = loans
                .Select(l => new CurrentLoanViewModel(
                    l.Title,
                    l.Authors.Count > 0 ? string.Join(", ", l.Authors) : "Chưa rõ tác giả",
                    l.DueDate))
                .ToList(),
            UnpaidFines = unpaidFines
        };
    }

    public async Task<ReaderPortalPageViewModel?> GetProfilePageAsync(Guid readerId, CancellationToken ct = default)
    {
        var baseModel = await BuildBasePortalModelAsync(readerId, ScreenId.ReaderProfile, ct);
        if (baseModel is null)
        {
            return null;
        }

        var activeLoans = await LoadLoansForReaderAsync(
            readerId,
            onlyActive: true,
            baseModel.Today,
            baseModel.OverdueFinePerDay,
            baseModel.MaxRenewals,
            baseModel.RenewDays,
            baseModel.CardStatus,
            baseModel.CardExpiryDate,
            baseModel.TotalUnpaidFine,
            ct);

        return WithActiveLoansAndSummary(baseModel, activeLoans);
    }

    public async Task<ReaderPortalPageViewModel?> GetBorrowHistoryPageAsync(
        Guid readerId,
        ReaderBorrowHistoryFilter filter,
        CancellationToken ct = default)
    {
        filter ??= new ReaderBorrowHistoryFilter();
        var baseModel = await BuildBasePortalModelAsync(readerId, ScreenId.ReaderBorrowHistory, ct);
        if (baseModel is null)
        {
            return null;
        }

        var allLoans = await LoadLoansForReaderAsync(
            readerId,
            onlyActive: false,
            baseModel.Today,
            baseModel.OverdueFinePerDay,
            baseModel.MaxRenewals,
            baseModel.RenewDays,
            baseModel.CardStatus,
            baseModel.CardExpiryDate,
            baseModel.TotalUnpaidFine,
            ct);

        var activeLoans = allLoans.Where(x => x.ReturnedAt == null).OrderBy(x => x.DueDate).ToList();

        IEnumerable<ReaderLoanItemViewModel> filtered = allLoans;

        if (!string.IsNullOrWhiteSpace(filter.LoanState))
        {
            var state = filter.LoanState.Trim().ToUpperInvariant();
            filtered = state switch
            {
                "BORROWING" => filtered.Where(x => x.ReturnedAt == null),
                "RETURNED" => filtered.Where(x => x.ReturnedAt != null),
                "OVERDUE" => filtered.Where(x =>
                    (x.ReturnedAt == null && x.DueDate < baseModel.Today) ||
                    (x.ReturnedAt != null && DateOnly.FromDateTime(x.ReturnedAt.Value.LocalDateTime) > x.DueDate)),
                _ => filtered
            };
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim();
            filtered = filtered.Where(x =>
                x.BookTitle.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                x.Authors.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                x.Barcode.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                x.SlipNo.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                x.ShelfCode.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        var filteredList = filtered.ToList();
        var totalCount = filteredList.Count;
        var pageItems = filteredList
            .Skip((filter.PageNo - 1) * filter.PageItemSize)
            .Take(filter.PageItemSize)
            .ToList();

        var pagedHistory = new PagedResult<ReaderLoanItemViewModel>(
            pageItems,
            totalCount,
            filter.PageNo,
            filter.PageItemSize);

        var enriched = WithActiveLoansAndSummary(baseModel, activeLoans);
        return new ReaderPortalPageViewModel
        {
            ActiveScreen = enriched.ActiveScreen,
            ReaderId = enriched.ReaderId,
            FullName = enriched.FullName,
            CardNo = enriched.CardNo,
            MemberCode = enriched.MemberCode,
            Email = enriched.Email,
            Phone = enriched.Phone,
            Address = enriched.Address,
            DateOfBirth = enriched.DateOfBirth,
            ReaderTypeName = enriched.ReaderTypeName,
            MaxBorrowBooks = enriched.MaxBorrowBooks,
            LoanDays = enriched.LoanDays,
            MaxRenewals = enriched.MaxRenewals,
            RenewDays = enriched.RenewDays,
            CardIssuedDate = enriched.CardIssuedDate,
            CardExpiryDate = enriched.CardExpiryDate,
            CardStatus = enriched.CardStatus,
            EffectiveCardBadgeCode = enriched.EffectiveCardBadgeCode,
            LockReason = enriched.LockReason,
            Today = enriched.Today,
            OverdueFinePerDay = enriched.OverdueFinePerDay,
            MaxUnpaidFine = enriched.MaxUnpaidFine,
            ActiveLoanCount = enriched.ActiveLoanCount,
            OverdueLoanCount = enriched.OverdueLoanCount,
            DueSoonLoanCount = enriched.DueSoonLoanCount,
            TotalHistoryCount = allLoans.Count,
            ReturnedLoanCount = allLoans.Count(x => x.ReturnedAt != null),
            TotalUnpaidFine = enriched.TotalUnpaidFine,
            EstimatedAccruingFine = enriched.EstimatedAccruingFine,
            TotalPaidFine = enriched.TotalPaidFine,
            TotalWaivedFine = enriched.TotalWaivedFine,
            UnpaidFineCount = enriched.UnpaidFineCount,
            ActiveReservationCount = enriched.ActiveReservationCount,
            ReadyReservationCount = enriched.ReadyReservationCount,
            Warnings = enriched.Warnings,
            ActiveLoans = activeLoans,
            BorrowHistory = pageItems,
            PagedBorrowHistory = pagedHistory,
            HistoryFilter = filter
        };
    }

    public async Task<ReaderPortalPageViewModel?> GetFinesPageAsync(
        Guid readerId,
        ReaderFineFilter filter,
        CancellationToken ct = default)
    {
        filter ??= new ReaderFineFilter();
        var baseModel = await BuildBasePortalModelAsync(readerId, ScreenId.ReaderFines, ct);
        if (baseModel is null)
        {
            return null;
        }

        var activeLoans = await LoadLoansForReaderAsync(
            readerId,
            onlyActive: true,
            baseModel.Today,
            baseModel.OverdueFinePerDay,
            baseModel.MaxRenewals,
            baseModel.RenewDays,
            baseModel.CardStatus,
            baseModel.CardExpiryDate,
            baseModel.TotalUnpaidFine,
            ct);

        var rawFines = await unitOfWork.Fines.GetQueryable(asNoTracking: true)
            .Where(f => f.Status != EntityStatus.Deleted
                        && f.BorrowDetail.Status != EntityStatus.Deleted
                        && f.BorrowDetail.BorrowSlip.Status != EntityStatus.Deleted
                        && f.BorrowDetail.BorrowSlip.ReaderId == readerId)
            .OrderBy(f => f.FineStatus == FineStatus.Unpaid ? 0 : 1)
            .ThenByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                f.Id,
                f.BorrowDetailId,
                BookId = f.BorrowDetail.BookCopy.BookId,
                SlipId = f.BorrowDetail.SlipId,
                BorrowedAt = f.BorrowDetail.BorrowSlip.BorrowedAt,
                DueDate = f.BorrowDetail.DueDate,
                ReturnedAt = f.BorrowDetail.ReturnedAt,
                Barcode = f.BorrowDetail.BookCopy.Barcode,
                BookTitle = f.BorrowDetail.BookCopy.Book.Title,
                Authors = f.BorrowDetail.BookCopy.Book.BookAuthors
                    .Where(ba => ba.Status != EntityStatus.Deleted)
                    .OrderBy(ba => ba.AuthorOrder)
                    .Select(ba => ba.Author.FullName)
                    .ToList(),
                f.FineType,
                f.OverdueDays,
                f.Amount,
                f.FineStatus,
                f.CreatedAt,
                f.PaidAt,
                f.PaymentMethod,
                f.Note
            })
            .ToListAsync(ct);

        var allFines = rawFines.Select(f => new ReaderFineItemViewModel
        {
            Id = f.Id,
            BorrowDetailId = f.BorrowDetailId,
            BookId = f.BookId,
            SlipNo = FormatSlipNo(f.SlipId, f.BorrowedAt),
            Barcode = f.Barcode,
            BookTitle = f.BookTitle,
            Authors = f.Authors.Count > 0 ? string.Join(", ", f.Authors) : "Chưa rõ tác giả",
            FineType = f.FineType,
            OverdueDays = f.OverdueDays,
            Amount = f.Amount,
            FineStatus = f.FineStatus,
            CreatedAt = f.CreatedAt,
            PaidAt = f.PaidAt,
            PaymentMethod = f.PaymentMethod,
            Note = f.Note,
            BorrowedAt = f.BorrowedAt,
            DueDate = f.DueDate,
            ReturnedAt = f.ReturnedAt
        }).ToList();

        IEnumerable<ReaderFineItemViewModel> filtered = allFines;

        if (!string.IsNullOrWhiteSpace(filter.FineStatus))
        {
            var status = filter.FineStatus.Trim().ToUpperInvariant();
            filtered = filtered.Where(f => string.Equals(f.FineStatus, status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.FineType))
        {
            var fType = filter.FineType.Trim().ToUpperInvariant();
            filtered = filtered.Where(f => string.Equals(f.FineType, fType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim();
            filtered = filtered.Where(f =>
                f.BookTitle.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                f.Authors.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                f.Barcode.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                f.SlipNo.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                (f.Note != null && f.Note.Contains(kw, StringComparison.OrdinalIgnoreCase)));
        }

        var filteredList = filtered.ToList();
        var totalCount = filteredList.Count;
        var pageItems = filteredList
            .Skip((filter.PageNo - 1) * filter.PageItemSize)
            .Take(filter.PageItemSize)
            .ToList();

        var pagedFines = new PagedResult<ReaderFineItemViewModel>(
            pageItems,
            totalCount,
            filter.PageNo,
            filter.PageItemSize);

        var enriched = WithActiveLoansAndSummary(baseModel, activeLoans);
        return new ReaderPortalPageViewModel
        {
            ActiveScreen = enriched.ActiveScreen,
            ReaderId = enriched.ReaderId,
            FullName = enriched.FullName,
            CardNo = enriched.CardNo,
            MemberCode = enriched.MemberCode,
            Email = enriched.Email,
            Phone = enriched.Phone,
            Address = enriched.Address,
            DateOfBirth = enriched.DateOfBirth,
            ReaderTypeName = enriched.ReaderTypeName,
            MaxBorrowBooks = enriched.MaxBorrowBooks,
            LoanDays = enriched.LoanDays,
            MaxRenewals = enriched.MaxRenewals,
            RenewDays = enriched.RenewDays,
            CardIssuedDate = enriched.CardIssuedDate,
            CardExpiryDate = enriched.CardExpiryDate,
            CardStatus = enriched.CardStatus,
            EffectiveCardBadgeCode = enriched.EffectiveCardBadgeCode,
            LockReason = enriched.LockReason,
            Today = enriched.Today,
            OverdueFinePerDay = enriched.OverdueFinePerDay,
            MaxUnpaidFine = enriched.MaxUnpaidFine,
            ActiveLoanCount = enriched.ActiveLoanCount,
            OverdueLoanCount = enriched.OverdueLoanCount,
            DueSoonLoanCount = enriched.DueSoonLoanCount,
            TotalHistoryCount = enriched.TotalHistoryCount,
            ReturnedLoanCount = enriched.ReturnedLoanCount,
            TotalUnpaidFine = allFines.Where(f => f.FineStatus == FineStatus.Unpaid).Sum(f => f.Amount),
            EstimatedAccruingFine = enriched.EstimatedAccruingFine,
            TotalPaidFine = allFines.Where(f => f.FineStatus == FineStatus.Paid).Sum(f => f.Amount),
            TotalWaivedFine = allFines.Where(f => f.FineStatus == FineStatus.Waived).Sum(f => f.Amount),
            UnpaidFineCount = allFines.Count(f => f.FineStatus == FineStatus.Unpaid),
            ActiveReservationCount = enriched.ActiveReservationCount,
            ReadyReservationCount = enriched.ReadyReservationCount,
            Warnings = enriched.Warnings,
            ActiveLoans = activeLoans,
            Fines = pageItems,
            PagedFines = pagedFines,
            FineFilter = filter
        };
    }

    public async Task<ReaderPortalPageViewModel?> GetReservationsPageAsync(Guid readerId, CancellationToken ct = default)
    {
        var baseModel = await BuildBasePortalModelAsync(readerId, ScreenId.ReaderReservations, ct);
        if (baseModel is null)
        {
            return null;
        }

        var activeLoans = await LoadLoansForReaderAsync(
            readerId,
            onlyActive: true,
            baseModel.Today,
            baseModel.OverdueFinePerDay,
            baseModel.MaxRenewals,
            baseModel.RenewDays,
            baseModel.CardStatus,
            baseModel.CardExpiryDate,
            baseModel.TotalUnpaidFine,
            ct);

        var rawReservations = await unitOfWork.Reservations.GetQueryable(asNoTracking: true)
            .Where(r => r.ReaderId == readerId && r.Status != EntityStatus.Deleted)
            .OrderBy(r => r.ReservationStatus == ReservationStatus.Ready ? 0
                        : r.ReservationStatus == ReservationStatus.Pending ? 1 : 2)
            .ThenByDescending(r => r.ReservedAt)
            .Select(r => new
            {
                r.Id,
                r.BookId,
                BookTitle = r.Book.Title,
                Authors = r.Book.BookAuthors
                    .Where(ba => ba.Status != EntityStatus.Deleted)
                    .OrderBy(ba => ba.AuthorOrder)
                    .Select(ba => ba.Author.FullName)
                    .ToList(),
                AssignedBarcode = r.AssignedCopy != null ? r.AssignedCopy.Barcode : null,
                ShelfCode = r.AssignedCopy != null && r.AssignedCopy.Shelf != null ? r.AssignedCopy.Shelf.Code : null,
                r.ReservationStatus,
                QueuePosition = r.ReservationStatus == ReservationStatus.Pending
                    ? r.Book.Reservations.Count(o => o.Status != EntityStatus.Deleted
                                                     && o.ReservationStatus == ReservationStatus.Pending
                                                     && o.ReservedAt < r.ReservedAt) + 1
                    : 0,
                r.ReservedAt,
                r.ReadyAt,
                r.ExpiresAt
            })
            .ToListAsync(ct);

        var reservations = rawReservations.Select(r => new ReaderReservationItemViewModel
        {
            Id = r.Id,
            BookId = r.BookId,
            BookTitle = r.BookTitle,
            Authors = r.Authors.Count > 0 ? string.Join(", ", r.Authors) : "Chưa rõ tác giả",
            AssignedBarcode = r.AssignedBarcode,
            ShelfCode = r.ShelfCode,
            ReservationStatus = r.ReservationStatus,
            QueuePosition = r.QueuePosition,
            ReservedAt = r.ReservedAt,
            ReadyAt = r.ReadyAt,
            ExpiresAt = r.ExpiresAt
        }).ToList();

        var enriched = WithActiveLoansAndSummary(baseModel, activeLoans);
        return new ReaderPortalPageViewModel
        {
            ActiveScreen = enriched.ActiveScreen,
            ReaderId = enriched.ReaderId,
            FullName = enriched.FullName,
            CardNo = enriched.CardNo,
            MemberCode = enriched.MemberCode,
            Email = enriched.Email,
            Phone = enriched.Phone,
            Address = enriched.Address,
            DateOfBirth = enriched.DateOfBirth,
            ReaderTypeName = enriched.ReaderTypeName,
            MaxBorrowBooks = enriched.MaxBorrowBooks,
            LoanDays = enriched.LoanDays,
            MaxRenewals = enriched.MaxRenewals,
            RenewDays = enriched.RenewDays,
            CardIssuedDate = enriched.CardIssuedDate,
            CardExpiryDate = enriched.CardExpiryDate,
            CardStatus = enriched.CardStatus,
            EffectiveCardBadgeCode = enriched.EffectiveCardBadgeCode,
            LockReason = enriched.LockReason,
            Today = enriched.Today,
            OverdueFinePerDay = enriched.OverdueFinePerDay,
            MaxUnpaidFine = enriched.MaxUnpaidFine,
            ActiveLoanCount = enriched.ActiveLoanCount,
            OverdueLoanCount = enriched.OverdueLoanCount,
            DueSoonLoanCount = enriched.DueSoonLoanCount,
            TotalHistoryCount = enriched.TotalHistoryCount,
            ReturnedLoanCount = enriched.ReturnedLoanCount,
            TotalUnpaidFine = enriched.TotalUnpaidFine,
            EstimatedAccruingFine = enriched.EstimatedAccruingFine,
            TotalPaidFine = enriched.TotalPaidFine,
            TotalWaivedFine = enriched.TotalWaivedFine,
            UnpaidFineCount = enriched.UnpaidFineCount,
            ActiveReservationCount = enriched.ActiveReservationCount,
            ReadyReservationCount = enriched.ReadyReservationCount,
            Warnings = enriched.Warnings,
            ActiveLoans = activeLoans,
            Reservations = reservations
        };
    }

    public async Task<ServiceResult> RenewLoanAsync(Guid readerId, Guid detailId, CancellationToken ct = default)
    {
        var today = dateTimeProvider.Today;

        var detail = await unitOfWork.BorrowDetails.GetQueryable(asNoTracking: false)
            .Include(d => d.BookCopy).ThenInclude(c => c.Book)
            .Include(d => d.BorrowSlip).ThenInclude(s => s.Reader).ThenInclude(r => r.ReaderType)
            .FirstOrDefaultAsync(d => d.Id == detailId
                                      && d.Status != EntityStatus.Deleted
                                      && d.BorrowSlip.ReaderId == readerId
                                      && d.BorrowSlip.Status != EntityStatus.Deleted, ct);

        if (detail is null)
        {
            return ServiceResult.Failure(ErrorCode.BorrowDetailNotFound);
        }

        if (detail.ReturnedAt != null)
        {
            return ServiceResult.Failure(ErrorCode.BookAlreadyReturned);
        }

        var reader = detail.BorrowSlip.Reader;
        if (reader.CardStatus == CardStatus.Locked)
        {
            return ServiceResult.Failure(ErrorCode.ReaderCardLocked);
        }

        if (reader.CardStatus == CardStatus.Expired || reader.CardExpiryDate < today)
        {
            return ServiceResult.Failure(ErrorCode.ReaderCardExpired);
        }

        var unpaid = await GetTotalUnpaidFineAsync(readerId, ct);
        if (unpaid > 0)
        {
            return ServiceResult.Failure(
                ErrorCode.ReaderHasUnpaidFines,
                ErrorMessages.Format(ErrorCode.ReaderHasUnpaidFines, CurrencyHelper.ToVnd(unpaid)));
        }

        if (detail.DueDate < today)
        {
            return ServiceResult.Failure(ErrorCode.RenewOverdueNotAllowed);
        }

        var readerType = reader.ReaderType;
        if (detail.RenewCount >= readerType.MaxRenewals)
        {
            return ServiceResult.Failure(
                ErrorCode.RenewalLimitReached,
                ErrorMessages.Format(ErrorCode.RenewalLimitReached, readerType.MaxRenewals));
        }

        var someoneWaiting = await unitOfWork.Reservations.GetQueryable(asNoTracking: true)
            .AnyAsync(r => r.Status != EntityStatus.Deleted
                           && r.BookId == detail.BookCopy.BookId
                           && r.ReaderId != readerId
                           && r.ReservationStatus == ReservationStatus.Pending, ct);

        if (someoneWaiting)
        {
            return ServiceResult.Failure(ErrorCode.RenewBlockedByReservation);
        }

        detail.DueDate = detail.DueDate.AddDays(readerType.RenewDays);
        detail.RenewCount++;
        await unitOfWork.SaveChangesAsync(ct);

        var remaining = readerType.MaxRenewals - detail.RenewCount;
        return ServiceResult.Success(
            $"Đã gia hạn tài liệu \"{detail.BookCopy.Book.Title}\" đến ngày {detail.DueDate.ToString(AppConstants.DefaultDateFormat)} (còn {remaining} lần gia hạn).");
    }

    public async Task<ServiceResult> CreateReservationAsync(Guid readerId, Guid bookId, CancellationToken ct = default)
    {
        var today = dateTimeProvider.Today;
        var reader = await unitOfWork.Readers.GetQueryable(asNoTracking: true)
            .Include(r => r.ReaderType)
            .FirstOrDefaultAsync(r => r.Id == readerId && r.Status != EntityStatus.Deleted, ct);

        if (reader is null)
        {
            return ServiceResult.Failure(ErrorCode.ReaderNotFound);
        }

        if (reader.CardStatus == CardStatus.Locked)
        {
            return ServiceResult.Failure(ErrorCode.ReaderCardLocked);
        }

        if (reader.CardStatus == CardStatus.Expired || reader.CardExpiryDate < today)
        {
            return ServiceResult.Failure(ErrorCode.ReaderCardExpired);
        }

        var book = await unitOfWork.Books.GetQueryable(asNoTracking: true)
            .Where(b => b.Id == bookId && b.Status != EntityStatus.Deleted)
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
            return ServiceResult.Failure(ErrorCode.BookNotFound);
        }

        if (book.HasAvailable)
        {
            return ServiceResult.Failure(ErrorCode.ReservationBookAvailable);
        }

        if (!book.HasCirculating)
        {
            return ServiceResult.Failure(ErrorCode.ReservationNotAvailable);
        }

        var alreadyBorrowing = await unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .AnyAsync(d => d.Status != EntityStatus.Deleted
                           && d.ReturnedAt == null
                           && d.BorrowSlip.ReaderId == readerId
                           && d.BorrowSlip.Status != EntityStatus.Deleted
                           && d.BookCopy.BookId == bookId, ct);

        if (alreadyBorrowing)
        {
            return ServiceResult.Failure(ErrorCode.ReaderAlreadyBorrowingBook);
        }

        var duplicate = await unitOfWork.Reservations.GetQueryable(asNoTracking: true)
            .AnyAsync(r => r.Status != EntityStatus.Deleted
                           && r.ReaderId == readerId
                           && r.BookId == bookId
                           && (r.ReservationStatus == ReservationStatus.Pending || r.ReservationStatus == ReservationStatus.Ready), ct);

        if (duplicate)
        {
            return ServiceResult.Failure(ErrorCode.ReservationDuplicate);
        }

        await unitOfWork.Reservations.AddAsync(new Reservation
        {
            Id = Guid.NewGuid(),
            ReaderId = readerId,
            BookId = bookId,
            ReservationStatus = ReservationStatus.Pending,
            ReservedAt = dateTimeProvider.UtcNow,
            Status = EntityStatus.Active
        }, ct);

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã đăng ký đặt trước tài liệu \"{book.Title}\". Khi có bản sao hoàn trả, hệ thống sẽ giữ chỗ cho bạn.");
    }

    public async Task<ServiceResult> CancelReservationAsync(Guid readerId, Guid reservationId, CancellationToken ct = default)
    {
        var reservation = await unitOfWork.Reservations.GetQueryable(asNoTracking: false)
            .Include(r => r.AssignedCopy)
            .Include(r => r.Book)
            .FirstOrDefaultAsync(r => r.Id == reservationId
                                      && r.ReaderId == readerId
                                      && r.Status != EntityStatus.Deleted, ct);

        if (reservation is null)
        {
            return ServiceResult.Failure(ErrorCode.ReservationNotFound);
        }

        if (reservation.ReservationStatus is not (ReservationStatus.Pending or ReservationStatus.Ready))
        {
            return ServiceResult.Failure(ErrorCode.ReservationAlreadyClosed);
        }

        var wasReady = reservation.ReservationStatus == ReservationStatus.Ready;
        var now = dateTimeProvider.UtcNow;
        reservation.ReservationStatus = ReservationStatus.Cancelled;
        reservation.ClosedAt = now;

        var setting = await GetSettingAsync(ct);
        if (wasReady && setting is not null && reservation.AssignedCopy is { CopyStatus: CopyStatus.Reserved } copy)
        {
            var next = await unitOfWork.Reservations.GetQueryable(asNoTracking: false)
                .Where(r => r.Status != EntityStatus.Deleted
                            && r.BookId == copy.BookId
                            && r.Id != reservation.Id
                            && r.ReservationStatus == ReservationStatus.Pending)
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
                next.ExpiresAt = now.AddHours(setting.ReservationHoldHours);
                copy.CopyStatus = CopyStatus.Reserved;
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã hủy yêu cầu đặt trước tài liệu \"{reservation.Book.Title}\".");
    }

    // ===== Helpers nội bộ =====

    private async Task<ReaderPortalPageViewModel?> BuildBasePortalModelAsync(
        Guid readerId,
        ScreenId activeScreen,
        CancellationToken ct)
    {
        var reader = await unitOfWork.Readers.GetQueryable(asNoTracking: true)
            .Include(r => r.ReaderType)
            .FirstOrDefaultAsync(r => r.Id == readerId && r.Status != EntityStatus.Deleted, ct);

        if (reader is null)
        {
            return null;
        }

        var today = dateTimeProvider.Today;
        var setting = await GetSettingAsync(ct);
        var finePerDay = setting?.OverdueFinePerDay ?? 2000m;
        var maxUnpaidFine = setting?.MaxUnpaidFine ?? 50000m;

        var fineStats = await unitOfWork.Fines.GetQueryable(asNoTracking: true)
            .Where(f => f.Status != EntityStatus.Deleted
                        && f.BorrowDetail.Status != EntityStatus.Deleted
                        && f.BorrowDetail.BorrowSlip.Status != EntityStatus.Deleted
                        && f.BorrowDetail.BorrowSlip.ReaderId == readerId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                UnpaidSum = g.Where(f => f.FineStatus == FineStatus.Unpaid).Sum(f => f.Amount),
                PaidSum = g.Where(f => f.FineStatus == FineStatus.Paid).Sum(f => f.Amount),
                WaivedSum = g.Where(f => f.FineStatus == FineStatus.Waived).Sum(f => f.Amount),
                UnpaidCount = g.Count(f => f.FineStatus == FineStatus.Unpaid)
            })
            .FirstOrDefaultAsync(ct);

        var loanCounts = await unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .Where(d => d.Status != EntityStatus.Deleted
                        && d.BorrowSlip.ReaderId == readerId
                        && d.BorrowSlip.Status != EntityStatus.Deleted)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Returned = g.Count(d => d.ReturnedAt != null)
            })
            .FirstOrDefaultAsync(ct);

        var reservationStats = await unitOfWork.Reservations.GetQueryable(asNoTracking: true)
            .Where(r => r.ReaderId == readerId
                        && r.Status != EntityStatus.Deleted
                        && (r.ReservationStatus == ReservationStatus.Pending || r.ReservationStatus == ReservationStatus.Ready))
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ActiveCount = g.Count(),
                ReadyCount = g.Count(r => r.ReservationStatus == ReservationStatus.Ready)
            })
            .FirstOrDefaultAsync(ct);

        var effectiveCardBadge = reader.CardStatus == CardStatus.Locked
            ? CardStatus.Locked
            : reader.CardStatus == CardStatus.Expired || reader.CardExpiryDate < today
                ? CardStatus.Expired
                : (reader.CardExpiryDate.DayNumber - today.DayNumber <= 30
                    ? StatusBadge.CardExpiringSoon
                    : CardStatus.Active);

        return new ReaderPortalPageViewModel
        {
            ActiveScreen = activeScreen,
            ReaderId = reader.Id,
            FullName = reader.FullName,
            CardNo = reader.CardNo,
            MemberCode = reader.MemberCode,
            Email = reader.Email,
            Phone = reader.Phone,
            Address = reader.Address,
            DateOfBirth = reader.DateOfBirth,
            ReaderTypeName = reader.ReaderType.Name,
            MaxBorrowBooks = reader.ReaderType.MaxBorrowBooks,
            LoanDays = reader.ReaderType.LoanDays,
            MaxRenewals = reader.ReaderType.MaxRenewals,
            RenewDays = reader.ReaderType.RenewDays,
            CardIssuedDate = reader.CardIssuedDate,
            CardExpiryDate = reader.CardExpiryDate,
            CardStatus = reader.CardStatus,
            EffectiveCardBadgeCode = effectiveCardBadge,
            LockReason = reader.LockReason,
            Today = today,
            OverdueFinePerDay = finePerDay,
            MaxUnpaidFine = maxUnpaidFine,
            TotalHistoryCount = loanCounts?.Total ?? 0,
            ReturnedLoanCount = loanCounts?.Returned ?? 0,
            TotalUnpaidFine = fineStats?.UnpaidSum ?? 0m,
            TotalPaidFine = fineStats?.PaidSum ?? 0m,
            TotalWaivedFine = fineStats?.WaivedSum ?? 0m,
            UnpaidFineCount = fineStats?.UnpaidCount ?? 0,
            ActiveReservationCount = reservationStats?.ActiveCount ?? 0,
            ReadyReservationCount = reservationStats?.ReadyCount ?? 0
        };
    }

    private async Task<List<ReaderLoanItemViewModel>> LoadLoansForReaderAsync(
        Guid readerId,
        bool onlyActive,
        DateOnly today,
        decimal finePerDay,
        short maxRenewals,
        short renewDays,
        string cardStatus,
        DateOnly cardExpiryDate,
        decimal totalUnpaidFine,
        CancellationToken ct)
    {
        var query = unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .Where(d => d.Status != EntityStatus.Deleted
                        && d.BorrowSlip.ReaderId == readerId
                        && d.BorrowSlip.Status != EntityStatus.Deleted);

        if (onlyActive)
        {
            query = query.Where(d => d.ReturnedAt == null)
                .OrderBy(d => d.DueDate)
                .ThenByDescending(d => d.BorrowSlip.BorrowedAt);
        }
        else
        {
            query = query
                .OrderBy(d => d.ReturnedAt == null ? 0 : 1)
                .ThenByDescending(d => d.BorrowSlip.BorrowedAt)
                .ThenByDescending(d => d.DueDate);
        }

        var rawItems = await query
            .Select(d => new
            {
                DetailId = d.Id,
                d.SlipId,
                BookId = d.BookCopy.BookId,
                d.BookCopy.Barcode,
                ShelfCode = d.BookCopy.Shelf != null ? d.BookCopy.Shelf.Code : "—",
                BookTitle = d.BookCopy.Book.Title,
                CoverUrl = d.BookCopy.Book.CoverUrl,
                Authors = d.BookCopy.Book.BookAuthors
                    .Where(ba => ba.Status != EntityStatus.Deleted)
                    .OrderBy(ba => ba.AuthorOrder)
                    .Select(ba => ba.Author.FullName)
                    .ToList(),
                d.BorrowSlip.BorrowedAt,
                d.DueDate,
                d.ReturnedAt,
                d.RenewCount,
                d.ReturnCondition,
                d.ReturnNote,
                ChargedOverdueDays = d.Fines
                    .Where(f => f.Status != EntityStatus.Deleted && f.FineType == FineType.Overdue && f.FineStatus != FineStatus.Cancelled)
                    .Sum(f => f.OverdueDays ?? 0),
                RecordedFine = d.Fines
                    .Where(f => f.Status != EntityStatus.Deleted && f.FineStatus != FineStatus.Cancelled)
                    .Sum(f => f.Amount),
                HasOtherPendingReservation = d.BookCopy.Book.Reservations
                    .Any(r => r.Status != EntityStatus.Deleted
                              && r.ReaderId != readerId
                              && r.ReservationStatus == ReservationStatus.Pending)
            })
            .ToListAsync(ct);

        return rawItems.Select(x =>
        {
            var isReturned = x.ReturnedAt != null;
            var daysLeft = x.DueDate.DayNumber - today.DayNumber;
            var statusCode = isReturned
                ? StatusBadge.Loan.Returned
                : daysLeft < 0
                    ? StatusBadge.Loan.Overdue
                    : daysLeft <= DueStamp.DueSoonDays
                        ? StatusBadge.Loan.DueSoon
                        : StatusBadge.Loan.Active;

            var unchargedLateDays = isReturned ? 0 : Math.Max(0, today.DayNumber - x.DueDate.DayNumber - x.ChargedOverdueDays);
            var estimatedFine = unchargedLateDays * finePerDay;

            var (canRenew, blockReason) = EvaluateRenewEligibility(
                isReturned,
                cardStatus,
                cardExpiryDate,
                today,
                totalUnpaidFine,
                x.DueDate,
                x.RenewCount,
                maxRenewals,
                x.HasOtherPendingReservation);

            return new ReaderLoanItemViewModel
            {
                DetailId = x.DetailId,
                SlipId = x.SlipId,
                BookId = x.BookId,
                SlipNo = FormatSlipNo(x.SlipId, x.BorrowedAt),
                Barcode = x.Barcode,
                ShelfCode = x.ShelfCode,
                BookTitle = x.BookTitle,
                Authors = x.Authors.Count > 0 ? string.Join(", ", x.Authors) : "Chưa rõ tác giả",
                CoverUrl = x.CoverUrl,
                BorrowedAt = x.BorrowedAt,
                DueDate = x.DueDate,
                ReturnedAt = x.ReturnedAt,
                RenewCount = x.RenewCount,
                MaxRenewals = maxRenewals,
                RenewDays = renewDays,
                ReturnCondition = x.ReturnCondition ?? ReturnCondition.Good,
                ReturnNote = x.ReturnNote,
                EstimatedOverdueFine = estimatedFine,
                RecordedFineAmount = x.RecordedFine,
                CanRenew = canRenew,
                RenewBlockReason = blockReason,
                LoanStatusCode = statusCode
            };
        }).ToList();
    }

    private static (bool CanRenew, string? Reason) EvaluateRenewEligibility(
        bool isReturned,
        string cardStatus,
        DateOnly cardExpiryDate,
        DateOnly today,
        decimal totalUnpaidFine,
        DateOnly dueDate,
        short renewCount,
        short maxRenewals,
        bool hasOtherPendingReservation)
    {
        if (isReturned) return (false, "Đã hoàn trả");
        if (cardStatus == CardStatus.Locked) return (false, "Thẻ đang bị khóa");
        if (cardStatus == CardStatus.Expired || cardExpiryDate < today) return (false, "Thẻ đã hết hạn");
        if (totalUnpaidFine > 0) return (false, "Còn nợ phạt chưa thanh toán");
        if (dueDate < today) return (false, "Sách đã quá hạn");
        if (renewCount >= maxRenewals) return (false, "Hết lượt gia hạn");
        if (hasOtherPendingReservation) return (false, "Đang có bạn đọc đặt trước");
        return (true, null);
    }

    private static ReaderPortalPageViewModel WithActiveLoansAndSummary(
        ReaderPortalPageViewModel model,
        List<ReaderLoanItemViewModel> activeLoans)
    {
        var overdueCount = activeLoans.Count(x => x.DueDate < model.Today);
        var dueSoonCount = activeLoans.Count(x => x.DueDate >= model.Today && x.DueDate.DayNumber - model.Today.DayNumber <= DueStamp.DueSoonDays);
        var estimatedAccruing = activeLoans.Sum(x => x.EstimatedOverdueFine);

        var warnings = new List<string>();
        if (model.CardStatus == CardStatus.Locked)
        {
            warnings.Add($"Thẻ thư viện của bạn đang bị khóa ({model.LockReason ?? "Vui lòng liên hệ quầy thủ thư"}).");
        }
        else if (model.CardStatus == CardStatus.Expired || model.CardExpiryDate < model.Today)
        {
            warnings.Add($"Thẻ thư viện đã hết hạn ngày {model.CardExpiryDate.ToString(AppConstants.DefaultDateFormat)}. Vui lòng đến quầy thủ thư để gia hạn thẻ.");
        }

        if (overdueCount > 0)
        {
            warnings.Add($"Bạn đang có {overdueCount} cuốn sách quá hạn trả. Vui lòng mang sách đến quầy thủ thư hoàn trả sớm để tránh phát sinh thêm phí phạt ({CurrencyHelper.ToVnd(model.OverdueFinePerDay)}/cuốn/ngày).");
        }

        if (model.TotalUnpaidFine > 0)
        {
            warnings.Add($"Bạn có khoản phạt {CurrencyHelper.ToVnd(model.TotalUnpaidFine)} chưa thanh toán. Theo quy định, bạn cần hoàn tất nộp phạt tại quầy trước khi mượn mới hoặc gia hạn sách.");
        }

        if (model.ReadyReservationCount > 0)
        {
            warnings.Add($"Có {model.ReadyReservationCount} đầu sách bạn đặt trước đã có bản sao sẵn sàng tại quầy! Vui lòng đến nhận trước khi hết thời gian giữ chỗ.");
        }

        return new ReaderPortalPageViewModel
        {
            ActiveScreen = model.ActiveScreen,
            ReaderId = model.ReaderId,
            FullName = model.FullName,
            CardNo = model.CardNo,
            MemberCode = model.MemberCode,
            Email = model.Email,
            Phone = model.Phone,
            Address = model.Address,
            DateOfBirth = model.DateOfBirth,
            ReaderTypeName = model.ReaderTypeName,
            MaxBorrowBooks = model.MaxBorrowBooks,
            LoanDays = model.LoanDays,
            MaxRenewals = model.MaxRenewals,
            RenewDays = model.RenewDays,
            CardIssuedDate = model.CardIssuedDate,
            CardExpiryDate = model.CardExpiryDate,
            CardStatus = model.CardStatus,
            EffectiveCardBadgeCode = model.EffectiveCardBadgeCode,
            LockReason = model.LockReason,
            Today = model.Today,
            OverdueFinePerDay = model.OverdueFinePerDay,
            MaxUnpaidFine = model.MaxUnpaidFine,
            ActiveLoanCount = activeLoans.Count,
            OverdueLoanCount = overdueCount,
            DueSoonLoanCount = dueSoonCount,
            TotalHistoryCount = model.TotalHistoryCount,
            ReturnedLoanCount = model.ReturnedLoanCount,
            TotalUnpaidFine = model.TotalUnpaidFine,
            EstimatedAccruingFine = estimatedAccruing,
            TotalPaidFine = model.TotalPaidFine,
            TotalWaivedFine = model.TotalWaivedFine,
            UnpaidFineCount = model.UnpaidFineCount,
            ActiveReservationCount = model.ActiveReservationCount,
            ReadyReservationCount = model.ReadyReservationCount,
            Warnings = warnings,
            ActiveLoans = activeLoans
        };
    }

    private Task<decimal> GetTotalUnpaidFineAsync(Guid readerId, CancellationToken ct) =>
        unitOfWork.Fines.GetQueryable(asNoTracking: true)
            .Where(f => f.Status != EntityStatus.Deleted
                        && f.FineStatus == FineStatus.Unpaid
                        && f.BorrowDetail.Status != EntityStatus.Deleted
                        && f.BorrowDetail.BorrowSlip.Status != EntityStatus.Deleted
                        && f.BorrowDetail.BorrowSlip.ReaderId == readerId)
            .SumAsync(f => f.Amount, ct);

    private Task<LibrarySetting?> GetSettingAsync(CancellationToken ct) =>
        unitOfWork.LibrarySettings.GetQueryable(asNoTracking: true)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(ct);

    private static string FormatSlipNo(Guid slipId, DateTimeOffset borrowedAt) =>
        $"PM-{borrowedAt:yyyyMMdd}-{slipId.ToString("N")[..6].ToUpperInvariant()}";
}
