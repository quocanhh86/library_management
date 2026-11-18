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
/// Triển khai Cổng Bạn Đọc Tự Phục Vụ theo chuẩn CQS (GET thuần đọc) và Policy dùng chung.
/// </summary>
public class ReaderPortalService(
    IUnitOfWork unitOfWork,
    ICirculationPolicy circulationPolicy,
    IReservationQueueEngine reservationQueueEngine,
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

        var unpaidFines = await circulationPolicy.GetUnpaidFinesAsync(readerId, ct);

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

        var activeLoans = await LoadLoansForReaderAsync(baseModel, onlyActive: true, ct);
        return CreatePortalPage(baseModel, activeLoans);
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

        var allLoans = await LoadLoansForReaderAsync(baseModel, onlyActive: false, ct);
        var activeLoans = allLoans.Where(x => x.ReturnedAt == null).OrderBy(x => x.DueDate).ToList();

        IEnumerable<ReaderLoanItemViewModel> filtered = allLoans;
        if (!string.IsNullOrWhiteSpace(filter.LoanState))
        {
            filtered = filter.LoanState.Trim().ToUpperInvariant() switch
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
        var pageItems = filteredList
            .Skip(filter.SkipCount)
            .Take(filter.TakeCount)
            .ToList();

        var pagedHistory = new PagedResult<ReaderLoanItemViewModel>(
            pageItems,
            filteredList.Count,
            filter.NormalizedPageNo,
            filter.PageItemSize);

        return CreatePortalPage(
            baseModel,
            activeLoans,
            pagedHistory: pagedHistory,
            historyFilter: filter,
            totalHistoryOverride: allLoans.Count,
            returnedCountOverride: allLoans.Count(x => x.ReturnedAt != null));
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

        var activeLoans = await LoadLoansForReaderAsync(baseModel, onlyActive: true, ct);

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
                f.BorrowDetail.SlipId,
                f.BorrowDetail.BorrowSlip.BorrowedAt,
                f.BorrowDetail.DueDate,
                f.BorrowDetail.ReturnedAt,
                f.BorrowDetail.BookCopy.Barcode,
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
            SlipNo = SlipCodeHelper.Format(f.SlipId, f.BorrowedAt),
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
        var pageItems = filteredList
            .Skip(filter.SkipCount)
            .Take(filter.TakeCount)
            .ToList();

        var pagedFines = new PagedResult<ReaderFineItemViewModel>(
            pageItems,
            filteredList.Count,
            filter.NormalizedPageNo,
            filter.PageItemSize);

        return CreatePortalPage(
            baseModel,
            activeLoans,
            pagedFines: pagedFines,
            fineFilter: filter,
            unpaidFineOverride: allFines.Where(f => f.FineStatus == FineStatus.Unpaid).Sum(f => f.Amount),
            paidFineOverride: allFines.Where(f => f.FineStatus == FineStatus.Paid).Sum(f => f.Amount),
            waivedFineOverride: allFines.Where(f => f.FineStatus == FineStatus.Waived).Sum(f => f.Amount),
            unpaidCountOverride: allFines.Count(f => f.FineStatus == FineStatus.Unpaid));
    }

    public async Task<ReaderPortalPageViewModel?> GetReservationsPageAsync(Guid readerId, CancellationToken ct = default)
    {
        var baseModel = await BuildBasePortalModelAsync(readerId, ScreenId.ReaderReservations, ct);
        if (baseModel is null)
        {
            return null;
        }

        var activeLoans = await LoadLoansForReaderAsync(baseModel, onlyActive: true, ct);

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

        return CreatePortalPage(baseModel, activeLoans, reservations: reservations);
    }

    public Task<ServiceResult> RenewLoanAsync(Guid readerId, Guid detailId, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
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

            var policyCheck = await circulationPolicy.ValidateRenewAsync(detail, ct);
            if (!policyCheck.IsSuccess)
            {
                return policyCheck;
            }

            var readerType = detail.BorrowSlip.Reader.ReaderType;
            detail.DueDate = detail.DueDate.AddDays(readerType.RenewDays);
            detail.RenewCount++;

            var remaining = readerType.MaxRenewals - detail.RenewCount;
            return ServiceResult.Success(
                $"Đã gia hạn tài liệu \"{detail.BookCopy.Book.Title}\" đến ngày {detail.DueDate.ToString(AppConstants.DefaultDateFormat)} (còn {remaining} lần gia hạn).");
        }, cancellationToken: ct);

    public Task<ServiceResult> CreateReservationAsync(Guid readerId, Guid bookId, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            await reservationQueueEngine.ExpireOverdueHoldsAsync(ct);

            var validation = await circulationPolicy.ValidateReservationAsync(readerId, bookId, ct);
            if (!validation.IsSuccess || validation.Data is null)
            {
                return ServiceResult.Failure(validation.Code ?? ErrorCode.ReservationNotAvailable, validation.Message);
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

            return ServiceResult.Success(
                $"Đã đăng ký đặt trước tài liệu \"{validation.Data.BookTitle}\". Khi có bản sao hoàn trả, hệ thống sẽ giữ chỗ cho bạn.");
        }, cancellationToken: ct);

    public Task<ServiceResult> CancelReservationAsync(Guid readerId, Guid reservationId, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
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
            reservation.ReservationStatus = ReservationStatus.Cancelled;
            reservation.ClosedAt = dateTimeProvider.UtcNow;

            if (wasReady && reservation.AssignedCopy is { CopyStatus: CopyStatus.Reserved } copy)
            {
                await reservationQueueEngine.AssignCopyToNextPendingOrReleaseAsync(
                    copy,
                    excludeReservationId: reservation.Id,
                    ct: ct);
            }

            return ServiceResult.Success($"Đã hủy yêu cầu đặt trước tài liệu \"{reservation.Book.Title}\".");
        }, cancellationToken: ct);

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
        var setting = await circulationPolicy.GetEffectiveSettingAsync(ct);

        var fineStats = await unitOfWork.Fines.GetQueryable(asNoTracking: true)
            .Where(f => f.Status != EntityStatus.Deleted
                        && f.BorrowDetail.Status != EntityStatus.Deleted
                        && f.BorrowDetail.BorrowSlip.Status != EntityStatus.Deleted
                        && f.BorrowDetail.BorrowSlip.ReaderId == readerId)
            .GroupBy(_ => 1)
            .OrderBy(g => g.Key)
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
            .OrderBy(g => g.Key)
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
            .OrderBy(g => g.Key)
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
            OverdueFinePerDay = setting.OverdueFinePerDay,
            MaxUnpaidFine = setting.MaxUnpaidFine,
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
        ReaderPortalPageViewModel baseModel,
        bool onlyActive,
        CancellationToken ct)
    {
        var query = unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .Where(d => d.Status != EntityStatus.Deleted
                        && d.BorrowSlip.ReaderId == baseModel.ReaderId
                        && d.BorrowSlip.Status != EntityStatus.Deleted);

        query = onlyActive
            ? query.Where(d => d.ReturnedAt == null)
                .OrderBy(d => d.DueDate)
                .ThenByDescending(d => d.BorrowSlip.BorrowedAt)
            : query
                .OrderBy(d => d.ReturnedAt == null ? 0 : 1)
                .ThenByDescending(d => d.BorrowSlip.BorrowedAt)
                .ThenByDescending(d => d.DueDate);

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
                              && r.ReaderId != baseModel.ReaderId
                              && r.ReservationStatus == ReservationStatus.Pending)
            })
            .ToListAsync(ct);

        var hasAnyOverdueLoan = rawItems.Any(x => x.ReturnedAt == null && x.DueDate < baseModel.Today);

        return rawItems.Select(x =>
        {
            var isReturned = x.ReturnedAt != null;
            var daysLeft = x.DueDate.DayNumber - baseModel.Today.DayNumber;
            var statusCode = isReturned
                ? StatusBadge.Loan.Returned
                : daysLeft < 0
                    ? StatusBadge.Loan.Overdue
                    : daysLeft <= DueStamp.DueSoonDays
                        ? StatusBadge.Loan.DueSoon
                        : StatusBadge.Loan.Active;

            var unchargedLateDays = isReturned ? 0 : Math.Max(0, baseModel.Today.DayNumber - x.DueDate.DayNumber - x.ChargedOverdueDays);
            var estimatedFine = unchargedLateDays * baseModel.OverdueFinePerDay;

            var (canRenew, blockReason) = EvaluateRenewEligibility(
                isReturned,
                baseModel.CardStatus,
                baseModel.CardExpiryDate,
                baseModel.Today,
                baseModel.TotalUnpaidFine,
                baseModel.MaxUnpaidFine,
                hasAnyOverdueLoan,
                x.DueDate,
                x.RenewCount,
                baseModel.MaxRenewals,
                x.HasOtherPendingReservation);

            return new ReaderLoanItemViewModel
            {
                DetailId = x.DetailId,
                SlipId = x.SlipId,
                BookId = x.BookId,
                SlipNo = SlipCodeHelper.Format(x.SlipId, x.BorrowedAt),
                Barcode = x.Barcode,
                ShelfCode = x.ShelfCode,
                BookTitle = x.BookTitle,
                Authors = x.Authors.Count > 0 ? string.Join(", ", x.Authors) : "Chưa rõ tác giả",
                CoverUrl = x.CoverUrl,
                BorrowedAt = x.BorrowedAt,
                DueDate = x.DueDate,
                ReturnedAt = x.ReturnedAt,
                RenewCount = x.RenewCount,
                MaxRenewals = baseModel.MaxRenewals,
                RenewDays = baseModel.RenewDays,
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
        bool hasAnyOverdueLoan,
        DateOnly dueDate,
        short renewCount,
        short maxRenewals,
        bool hasOtherPendingReservation)
    {
        if (isReturned) return (false, "Đã hoàn trả");
        if (cardStatus == CardStatus.Locked) return (false, "Thẻ đang bị khóa");
        if (cardStatus == CardStatus.Expired || cardExpiryDate < today) return (false, "Thẻ đã hết hạn");
        if (dueDate < today) return (false, "Sách đã quá hạn");
        if (hasAnyOverdueLoan) return (false, "Đang có sách quá hạn chưa trả");
        if (totalUnpaidFine >= maxUnpaidFine) return (false, $"Nợ phạt vượt hạn mức ({CurrencyHelper.ToVnd(maxUnpaidFine)})");
        if (renewCount >= maxRenewals) return (false, $"Đã hết lượt gia hạn ({renewCount}/{maxRenewals} lần)");
        if (hasOtherPendingReservation) return (false, "Đang có bạn đọc đặt trước");
        return (true, null);
    }

    private static ReaderPortalPageViewModel CreatePortalPage(
        ReaderPortalPageViewModel baseModel,
        List<ReaderLoanItemViewModel> activeLoans,
        PagedResult<ReaderLoanItemViewModel>? pagedHistory = null,
        ReaderBorrowHistoryFilter? historyFilter = null,
        int? totalHistoryOverride = null,
        int? returnedCountOverride = null,
        PagedResult<ReaderFineItemViewModel>? pagedFines = null,
        ReaderFineFilter? fineFilter = null,
        decimal? unpaidFineOverride = null,
        decimal? paidFineOverride = null,
        decimal? waivedFineOverride = null,
        int? unpaidCountOverride = null,
        List<ReaderReservationItemViewModel>? reservations = null)
    {
        var overdueCount = activeLoans.Count(x => x.DueDate < baseModel.Today);
        var dueSoonCount = activeLoans.Count(x =>
            x.DueDate >= baseModel.Today &&
            x.DueDate.DayNumber - baseModel.Today.DayNumber <= DueStamp.DueSoonDays);
        var estimatedAccruing = activeLoans.Sum(x => x.EstimatedOverdueFine);
        var unpaidFine = unpaidFineOverride ?? baseModel.TotalUnpaidFine;

        var warnings = BuildPortalWarnings(baseModel, overdueCount, unpaidFine);

        return new ReaderPortalPageViewModel
        {
            ActiveScreen = baseModel.ActiveScreen,
            ReaderId = baseModel.ReaderId,
            FullName = baseModel.FullName,
            CardNo = baseModel.CardNo,
            MemberCode = baseModel.MemberCode,
            Email = baseModel.Email,
            Phone = baseModel.Phone,
            Address = baseModel.Address,
            DateOfBirth = baseModel.DateOfBirth,
            ReaderTypeName = baseModel.ReaderTypeName,
            MaxBorrowBooks = baseModel.MaxBorrowBooks,
            LoanDays = baseModel.LoanDays,
            MaxRenewals = baseModel.MaxRenewals,
            RenewDays = baseModel.RenewDays,
            CardIssuedDate = baseModel.CardIssuedDate,
            CardExpiryDate = baseModel.CardExpiryDate,
            CardStatus = baseModel.CardStatus,
            EffectiveCardBadgeCode = baseModel.EffectiveCardBadgeCode,
            LockReason = baseModel.LockReason,
            Today = baseModel.Today,
            OverdueFinePerDay = baseModel.OverdueFinePerDay,
            MaxUnpaidFine = baseModel.MaxUnpaidFine,
            ActiveLoanCount = activeLoans.Count,
            OverdueLoanCount = overdueCount,
            DueSoonLoanCount = dueSoonCount,
            TotalHistoryCount = totalHistoryOverride ?? baseModel.TotalHistoryCount,
            ReturnedLoanCount = returnedCountOverride ?? baseModel.ReturnedLoanCount,
            TotalUnpaidFine = unpaidFine,
            EstimatedAccruingFine = estimatedAccruing,
            TotalPaidFine = paidFineOverride ?? baseModel.TotalPaidFine,
            TotalWaivedFine = waivedFineOverride ?? baseModel.TotalWaivedFine,
            UnpaidFineCount = unpaidCountOverride ?? baseModel.UnpaidFineCount,
            ActiveReservationCount = baseModel.ActiveReservationCount,
            ReadyReservationCount = baseModel.ReadyReservationCount,
            Warnings = warnings,
            ActiveLoans = activeLoans,
            BorrowHistory = pagedHistory != null ? [.. pagedHistory.Items] : [],
            PagedBorrowHistory = pagedHistory ?? new(),
            HistoryFilter = historyFilter ?? new(),
            Fines = pagedFines != null ? [.. pagedFines.Items] : [],
            PagedFines = pagedFines ?? new(),
            FineFilter = fineFilter ?? new(),
            Reservations = reservations ?? []
        };
    }

    private static List<string> BuildPortalWarnings(ReaderPortalPageViewModel model, int overdueCount, decimal unpaidFine)
    {
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

        if (unpaidFine >= model.MaxUnpaidFine)
        {
            warnings.Add($"Bạn có khoản phạt {CurrencyHelper.ToVnd(unpaidFine)} vượt hạn mức cho phép ({CurrencyHelper.ToVnd(model.MaxUnpaidFine)}). Vui lòng hoàn tất nộp phạt tại quầy để mở khóa quyền mượn mới và gia hạn sách.");
        }
        else if (unpaidFine > 0)
        {
            warnings.Add($"Bạn đang có khoản phạt {CurrencyHelper.ToVnd(unpaidFine)} chưa thanh toán. Bạn vẫn được gia hạn sách đang mượn (dưới hạn mức {CurrencyHelper.ToVnd(model.MaxUnpaidFine)}), vui lòng nộp phạt tại quầy lưu hành trong lần đến thư viện gần nhất.");
        }

        if (model.ReadyReservationCount > 0)
        {
            warnings.Add($"Có {model.ReadyReservationCount} đầu sách bạn đặt trước đã có bản sao sẵn sàng tại quầy! Vui lòng đến nhận trước khi hết thời gian giữ chỗ.");
        }

        return warnings;
    }
}
