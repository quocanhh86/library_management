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

        await EnsureReaderCleanDemoFlowAsync(readerId, ct);

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
            baseModel.MaxUnpaidFine,
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
            baseModel.MaxUnpaidFine,
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
            baseModel.MaxUnpaidFine,
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
            baseModel.MaxUnpaidFine,
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

        var setting = await GetSettingAsync(ct);
        var maxUnpaidFine = setting?.MaxUnpaidFine ?? 50000m;
        var unpaid = await GetTotalUnpaidFineAsync(readerId, ct);
        if (unpaid >= maxUnpaidFine)
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

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var reader = await unitOfWork.Readers.GetQueryable(asNoTracking: true)
                .Include(r => r.ReaderType)
                .FirstOrDefaultAsync(r => r.Id == readerId && r.Status == EntityStatus.Active, ct);

            if (reader is null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReaderNotFound);
            }

            if (reader.CardStatus == CardStatus.Locked)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReaderCardLocked);
            }

            if (reader.CardStatus == CardStatus.Expired || reader.CardExpiryDate < today)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReaderCardExpired);
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

            var alreadyBorrowing = await unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
                .AnyAsync(d => d.Status != EntityStatus.Deleted
                               && d.ReturnedAt == null
                               && d.BorrowSlip.ReaderId == readerId
                               && d.BorrowSlip.Status != EntityStatus.Deleted
                               && d.BookCopy.BookId == bookId, ct);

            if (alreadyBorrowing)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ReaderAlreadyBorrowingBook);
            }

            var duplicate = await unitOfWork.Reservations.GetQueryable(asNoTracking: true)
                .AnyAsync(r => r.Status != EntityStatus.Deleted
                               && r.ReaderId == readerId
                               && r.BookId == bookId
                               && (r.ReservationStatus == ReservationStatus.Pending || r.ReservationStatus == ReservationStatus.Ready), ct);

            if (duplicate)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
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

            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã đăng ký đặt trước tài liệu \"{book.Title}\". Khi có bản sao hoàn trả, hệ thống sẽ giữ chỗ cho bạn.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> CancelReservationAsync(Guid readerId, Guid reservationId, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var reservation = await unitOfWork.Reservations.GetQueryable(asNoTracking: false)
                .Include(r => r.AssignedCopy)
                .Include(r => r.Book)
                .FirstOrDefaultAsync(r => r.Id == reservationId
                                          && r.ReaderId == readerId
                                          && r.Status != EntityStatus.Deleted, ct);

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
                                && r.Reader.Status == EntityStatus.Active
                                && r.Book.Status == EntityStatus.Active
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

            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã hủy yêu cầu đặt trước tài liệu \"{reservation.Book.Title}\".");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // ===== Helpers nội bộ =====

    private async Task<ReaderPortalPageViewModel?> BuildBasePortalModelAsync(
        Guid readerId,
        ScreenId activeScreen,
        CancellationToken ct)
    {
        await EnsureReaderCleanDemoFlowAsync(readerId, ct);

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
        decimal maxUnpaidFine,
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
                maxUnpaidFine,
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
        decimal maxUnpaidFine,
        DateOnly dueDate,
        short renewCount,
        short maxRenewals,
        bool hasOtherPendingReservation)
    {
        if (isReturned) return (false, "Đã hoàn trả");
        if (cardStatus == CardStatus.Locked) return (false, "Thẻ đang bị khóa");
        if (cardStatus == CardStatus.Expired || cardExpiryDate < today) return (false, "Thẻ đã hết hạn");
        if (totalUnpaidFine >= maxUnpaidFine) return (false, $"Nợ phạt vượt hạn mức ({CurrencyHelper.ToVnd(maxUnpaidFine)})");
        if (dueDate < today) return (false, "Sách đã quá hạn");
        if (renewCount >= maxRenewals) return (false, $"Đã hết lượt gia hạn ({renewCount}/{maxRenewals} lần)");
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

        if (model.TotalUnpaidFine >= model.MaxUnpaidFine)
        {
            warnings.Add($"Bạn có khoản phạt {CurrencyHelper.ToVnd(model.TotalUnpaidFine)} vượt hạn mức cho phép ({CurrencyHelper.ToVnd(model.MaxUnpaidFine)}). Vui lòng hoàn tất nộp phạt tại quầy để mở khóa quyền mượn mới và gia hạn sách.");
        }
        else if (model.TotalUnpaidFine > 0)
        {
            warnings.Add($"Bạn đang có khoản phạt {CurrencyHelper.ToVnd(model.TotalUnpaidFine)} chưa thanh toán. Bạn vẫn được gia hạn sách đang mượn (dưới hạn mức {CurrencyHelper.ToVnd(model.MaxUnpaidFine)}), vui lòng nộp phạt tại quầy lưu hành trong lần đến thư viện gần nhất.");
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

    /// <summary>
    /// Tự động khởi tạo bộ dữ liệu mẫu sạch, đầy đủ 4 luồng nghiệp vụ (Sách đang mượn, Lịch sử mượn, Tiền phạt, Sách đặt trước)
    /// cho tài khoản bạn đọc đang đăng nhập nếu chưa có dữ liệu demo V2.
    /// </summary>
    private async Task EnsureReaderCleanDemoFlowAsync(Guid readerId, CancellationToken ct)
    {
        const string demoMarker = "Phiếu mượn tổng hợp học kỳ [DEMO-FULL-FLOW-V2]";

        var alreadySeeded = await unitOfWork.BorrowSlips.GetQueryable(asNoTracking: true)
            .AnyAsync(s => s.ReaderId == readerId && s.Note == demoMarker, ct);
        if (alreadySeeded)
        {
            return;
        }

        var reader = await unitOfWork.Readers.GetQueryable(asNoTracking: true)
            .FirstOrDefaultAsync(r => r.Id == readerId && r.Status != EntityStatus.Deleted, ct);
        if (reader is null)
        {
            return;
        }

        var staffId = await unitOfWork.Staff.GetQueryable(asNoTracking: true)
            .Where(s => s.Status != EntityStatus.Deleted)
            .OrderBy(s => s.CreatedAt)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(ct);

        var books = await unitOfWork.Books.GetQueryable(asNoTracking: true)
            .Where(b => b.Status != EntityStatus.Deleted)
            .OrderBy(b => b.CreatedAt)
            .Select(b => b.Id)
            .ToListAsync(ct);

        var shelves = await unitOfWork.Shelves.GetQueryable(asNoTracking: true)
            .Where(s => s.Status != EntityStatus.Deleted)
            .OrderBy(s => s.Code)
            .Select(s => s.Id)
            .ToListAsync(ct);

        if (staffId == Guid.Empty || books.Count < 4 || shelves.Count == 0)
        {
            return;
        }

        var now = dateTimeProvider.UtcNow;
        var today = dateTimeProvider.Today;
        var shortTag = readerId.ToString("N")[..4].ToUpperInvariant();

        async Task<BookCopy> GetOrCreateDedicatedCopyAsync(int index, Guid bookId, string copyStatus)
        {
            var barcode = $"BC-26{shortTag}-{index:D2}";
            var existing = await unitOfWork.BookCopies.GetQueryable(asNoTracking: false)
                .FirstOrDefaultAsync(c => c.Barcode == barcode, ct);
            if (existing != null)
            {
                if (existing.CopyStatus != copyStatus)
                {
                    existing.CopyStatus = copyStatus;
                    await unitOfWork.SaveChangesAsync(ct);
                }
                return existing;
            }

            var copy = new BookCopy
            {
                Id = Guid.NewGuid(),
                BookId = bookId,
                ShelfId = shelves[index % shelves.Count],
                Barcode = barcode,
                CopyStatus = copyStatus,
                Price = 145000m,
                AcquiredDate = today.AddMonths(-5),
                Status = EntityStatus.Active,
                CreatedAt = now.AddMonths(-5),
                UpdatedAt = now,
                CreatedBy = staffId
            };
            await unitOfWork.BookCopies.AddAsync(copy, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return copy;
        }

        // 1. Bổ sung sách đang mượn để đủ 3 kịch bản: Trong hạn (0/2), Sắp đến hạn (1/2), Đã hết lượt gia hạn (2/2)
        var existingActiveCount = await unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .CountAsync(d => d.BorrowSlip.ReaderId == readerId && d.ReturnedAt == null && d.Status != EntityStatus.Deleted, ct);

        var activeSlip = new BorrowSlip
        {
            Id = Guid.NewGuid(),
            ReaderId = readerId,
            IssuedBy = staffId,
            BorrowedAt = now.AddDays(-6),
            Note = demoMarker,
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-6),
            UpdatedAt = now.AddDays(-2),
            CreatedBy = staffId
        };
        await unitOfWork.BorrowSlips.AddAsync(activeSlip, ct);

        if (existingActiveCount == 0)
        {
            var copyActiveNormal = await GetOrCreateDedicatedCopyAsync(1, books[0], CopyStatus.Available);
            await unitOfWork.BorrowDetails.AddAsync(new BorrowDetail
            {
                Id = Guid.NewGuid(),
                SlipId = activeSlip.Id,
                CopyId = copyActiveNormal.Id,
                DueDate = today.AddDays(9),
                RenewCount = 0,
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-5),
                CreatedBy = staffId
            }, ct);
        }

        if (existingActiveCount <= 1)
        {
            var copyDueSoon = await GetOrCreateDedicatedCopyAsync(2, books[1 % books.Count], CopyStatus.Available);
            var copyMaxRenew = await GetOrCreateDedicatedCopyAsync(3, books[2 % books.Count], CopyStatus.Available);

            await unitOfWork.BorrowDetails.AddRangeAsync(
            [
                new BorrowDetail
                {
                    Id = Guid.NewGuid(),
                    SlipId = activeSlip.Id,
                    CopyId = copyDueSoon.Id,
                    DueDate = today.AddDays(2), // Sắp đến hạn (còn 2 ngày), gia hạn 1/2 -> Bấm gia hạn tiếp được
                    RenewCount = 1,
                    Status = EntityStatus.Active,
                    CreatedAt = now.AddDays(-12),
                    UpdatedAt = now.AddDays(-5),
                    CreatedBy = staffId
                },
                new BorrowDetail
                {
                    Id = Guid.NewGuid(),
                    SlipId = activeSlip.Id,
                    CopyId = copyMaxRenew.Id,
                    DueDate = today.AddDays(5), // Đã gia hạn tối đa 2/2 lần -> Minh họa chặn gia hạn
                    RenewCount = 2,
                    Status = EntityStatus.Active,
                    CreatedAt = now.AddDays(-16),
                    UpdatedAt = now.AddDays(-2),
                    CreatedBy = staffId
                }
            ], ct);
        }

        // 2. Bổ sung Lịch sử đã hoàn trả & Đầy đủ 3 trạng thái Tiền phạt (UNPAID, PAID, WAIVED) + 2 loại phạt (OVERDUE, DAMAGED)
        var copyHistA = await GetOrCreateDedicatedCopyAsync(4, books[3 % books.Count], CopyStatus.Available);
        var copyHistB = await GetOrCreateDedicatedCopyAsync(5, books[4 % books.Count], CopyStatus.Available);
        var copyHistC = await GetOrCreateDedicatedCopyAsync(6, books[5 % books.Count], CopyStatus.Available);

        var historySlip = new BorrowSlip
        {
            Id = Guid.NewGuid(),
            ReaderId = readerId,
            IssuedBy = staffId,
            BorrowedAt = now.AddDays(-28),
            Note = "Phiếu mượn tài liệu chuyên ngành (Đã hoàn trả)",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-28),
            UpdatedAt = now.AddDays(-6),
            CreatedBy = staffId
        };

        var detailLateUnpaid = new BorrowDetail
        {
            Id = Guid.NewGuid(),
            SlipId = historySlip.Id,
            CopyId = copyHistA.Id,
            DueDate = today.AddDays(-10),
            ReturnedAt = now.AddDays(-7),
            ReceivedBy = staffId,
            RenewCount = 0,
            ReturnCondition = ReturnCondition.Good,
            ReturnNote = "Trả trễ 3 ngày, lập phiếu thu phạt quá hạn",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-24),
            UpdatedAt = now.AddDays(-7),
            CreatedBy = staffId
        };

        var detailDamagedPaid = new BorrowDetail
        {
            Id = Guid.NewGuid(),
            SlipId = historySlip.Id,
            CopyId = copyHistB.Id,
            DueDate = today.AddDays(-22),
            ReturnedAt = now.AddDays(-23),
            ReceivedBy = staffId,
            RenewCount = 1,
            ReturnCondition = ReturnCondition.Damaged,
            ReturnNote = "Rách nhẹ góc bìa phụ, đã thu phí bồi thường tại quầy",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-28),
            UpdatedAt = now.AddDays(-23),
            CreatedBy = staffId
        };

        var detailWaived = new BorrowDetail
        {
            Id = Guid.NewGuid(),
            SlipId = historySlip.Id,
            CopyId = copyHistC.Id,
            DueDate = today.AddDays(-30),
            ReturnedAt = now.AddDays(-29),
            ReceivedBy = staffId,
            RenewCount = 0,
            ReturnCondition = ReturnCondition.Good,
            ReturnNote = "Trễ 1 ngày do trùng lịch nghỉ lễ toàn trường",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-44),
            UpdatedAt = now.AddDays(-29),
            CreatedBy = staffId
        };

        await unitOfWork.BorrowSlips.AddAsync(historySlip, ct);
        await unitOfWork.BorrowDetails.AddRangeAsync([detailLateUnpaid, detailDamagedPaid, detailWaived], ct);

        var hasUnpaidFine = await unitOfWork.Fines.GetQueryable(asNoTracking: true)
            .AnyAsync(f => f.BorrowDetail.BorrowSlip.ReaderId == readerId && f.FineStatus == FineStatus.Unpaid, ct);
        var hasPaidOverdueFine = await unitOfWork.Fines.GetQueryable(asNoTracking: true)
            .AnyAsync(f => f.BorrowDetail.BorrowSlip.ReaderId == readerId && f.FineStatus == FineStatus.Paid && f.FineType == FineType.Overdue, ct);

        var newFines = new List<Fine>
        {
            new()
            {
                Id = Guid.NewGuid(),
                BorrowDetailId = detailDamagedPaid.Id,
                FineType = FineType.Damaged,
                OverdueDays = null,
                Amount = 15000m,
                FineStatus = FineStatus.Paid,
                HandledBy = staffId,
                PaidAt = now.AddDays(-23),
                PaymentMethod = PaymentMethod.Cash,
                Note = "Phí bồi thường hư hỏng nhẹ bìa phụ tài liệu (đã thu tiền mặt tại quầy)",
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-23),
                UpdatedAt = now.AddDays(-23),
                CreatedBy = staffId
            },
            new()
            {
                Id = Guid.NewGuid(),
                BorrowDetailId = detailWaived.Id,
                FineType = FineType.Overdue,
                OverdueDays = 1,
                Amount = 2000m,
                FineStatus = FineStatus.Waived,
                HandledBy = staffId,
                PaidAt = null,
                PaymentMethod = null,
                Note = "Miễn giảm phí quá hạn 1 ngày do trùng kỳ nghỉ lễ của trường",
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-29),
                UpdatedAt = now.AddDays(-29),
                CreatedBy = staffId
            }
        };

        if (!hasPaidOverdueFine)
        {
            var copyHistD = await GetOrCreateDedicatedCopyAsync(8, books[0], CopyStatus.Available);
            var detailLatePaid = new BorrowDetail
            {
                Id = Guid.NewGuid(),
                SlipId = historySlip.Id,
                CopyId = copyHistD.Id,
                DueDate = today.AddDays(-21),
                ReturnedAt = now.AddDays(-17),
                ReceivedBy = staffId,
                RenewCount = 0,
                ReturnCondition = ReturnCondition.Good,
                ReturnNote = "Trả trễ 4 ngày, đã nộp phạt tại quầy",
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-35),
                UpdatedAt = now.AddDays(-17),
                CreatedBy = staffId
            };
            await unitOfWork.BorrowDetails.AddAsync(detailLatePaid, ct);

            newFines.Add(new Fine
            {
                Id = Guid.NewGuid(),
                BorrowDetailId = detailLatePaid.Id,
                FineType = FineType.Overdue,
                OverdueDays = 4,
                Amount = 8000m,
                FineStatus = FineStatus.Paid,
                HandledBy = staffId,
                PaidAt = now.AddDays(-17),
                PaymentMethod = PaymentMethod.BankTransfer,
                Note = "Đã thu tiền phạt quá hạn 4 ngày qua chuyển khoản tại quầy lưu hành",
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-17),
                UpdatedAt = now.AddDays(-17),
                CreatedBy = staffId
            });
        }

        if (!hasUnpaidFine)
        {
            newFines.Add(new Fine
            {
                Id = Guid.NewGuid(),
                BorrowDetailId = detailLateUnpaid.Id,
                FineType = FineType.Overdue,
                OverdueDays = 3,
                Amount = 6000m,
                FineStatus = FineStatus.Unpaid,
                HandledBy = null,
                PaidAt = null,
                PaymentMethod = null,
                Note = "Phạt trả sách quá hạn 3 ngày (2.000 đ/ngày), chờ bạn đọc nộp tại quầy lưu hành",
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-7),
                UpdatedAt = now.AddDays(-7),
                CreatedBy = staffId
            });
        }

        await unitOfWork.Fines.AddRangeAsync(newFines, ct);

        // 3. Bổ sung Sách đặt trước (1 cuốn READY có mã vạch giữ tại quầy + 1 cuốn PENDING xếp hàng chờ #1)
        var existingResBookIds = await unitOfWork.Reservations.GetQueryable(asNoTracking: true)
            .Where(r => r.ReaderId == readerId && r.Status != EntityStatus.Deleted)
            .Select(r => r.BookId)
            .ToListAsync(ct);

        var candidateBooks = books.Where(b => !existingResBookIds.Contains(b)).ToList();
        if (existingResBookIds.Count == 0 && candidateBooks.Count >= 2)
        {
            var bookReadyId = candidateBooks[^1];
            var bookPendingId = candidateBooks[^2];
            var copyReserved = await GetOrCreateDedicatedCopyAsync(7, bookReadyId, CopyStatus.Reserved);

            await unitOfWork.Reservations.AddRangeAsync(
            [
                new Reservation
                {
                    Id = Guid.NewGuid(),
                    ReaderId = readerId,
                    BookId = bookReadyId,
                    AssignedCopyId = copyReserved.Id,
                    ReservationStatus = ReservationStatus.Ready,
                    ReservedAt = now.AddHours(-16),
                    ReadyAt = now.AddHours(-2),
                    ExpiresAt = now.AddHours(46),
                    ClosedAt = null,
                    Status = EntityStatus.Active,
                    CreatedAt = now.AddHours(-16),
                    UpdatedAt = now.AddHours(-2),
                    CreatedBy = staffId
                },
                new Reservation
                {
                    Id = Guid.NewGuid(),
                    ReaderId = readerId,
                    BookId = bookPendingId,
                    AssignedCopyId = null,
                    ReservationStatus = ReservationStatus.Pending,
                    ReservedAt = now.AddHours(-5),
                    ReadyAt = null,
                    ExpiresAt = null,
                    ClosedAt = null,
                    Status = EntityStatus.Active,
                    CreatedAt = now.AddHours(-5),
                    UpdatedAt = now.AddHours(-5),
                    CreatedBy = staffId
                }
            ], ct);
        }
        else if (existingResBookIds.Count == 1 && candidateBooks.Count >= 1)
        {
            var bookPendingId = candidateBooks[^1];
            await unitOfWork.Reservations.AddAsync(new Reservation
            {
                Id = Guid.NewGuid(),
                ReaderId = readerId,
                BookId = bookPendingId,
                AssignedCopyId = null,
                ReservationStatus = ReservationStatus.Pending,
                ReservedAt = now.AddHours(-5),
                ReadyAt = null,
                ExpiresAt = null,
                ClosedAt = null,
                Status = EntityStatus.Active,
                CreatedAt = now.AddHours(-5),
                UpdatedAt = now.AddHours(-5),
                CreatedBy = staffId
            }, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}
