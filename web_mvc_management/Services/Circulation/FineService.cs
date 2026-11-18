using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Extensions;
using web_mvc_management.Helper;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Circulation;
using web_mvc_management.Repositories;
using static web_mvc_management.Constant.LibraryConstants;

namespace web_mvc_management.Services;

public class FineService(
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : IFineService
{
    public async Task<FinesPageViewModel> GetFinesPageAsync(FineFilter filter, CancellationToken ct = default)
    {
        var query = unitOfWork.Fines.GetQueryable(asNoTracking: true)
            .Where(f => f.Status != EntityStatus.Deleted
                        && f.BorrowDetail.Status != EntityStatus.Deleted
                        && f.BorrowDetail.BorrowSlip.Status != EntityStatus.Deleted);

        if (!string.IsNullOrWhiteSpace(filter.FineStatus))
        {
            query = query.Where(f => f.FineStatus == filter.FineStatus);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.ToLower();
            query = query.Where(f =>
                f.BorrowDetail.BorrowSlip.Reader.FullName.ToLower().Contains(kw) ||
                f.BorrowDetail.BorrowSlip.Reader.CardNo.ToLower().Contains(kw));
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
            .Where(f => f.Status != EntityStatus.Deleted
                        && f.FineStatus == FineStatus.Unpaid
                        && f.BorrowDetail.Status != EntityStatus.Deleted
                        && f.BorrowDetail.BorrowSlip.Status != EntityStatus.Deleted)
            .SumAsync(f => f.Amount, ct);

        return new FinesPageViewModel
        {
            Fines = paged,
            Filter = filter,
            UnpaidTotal = unpaidTotal
        };
    }

    public Task<ServiceResult> PayFineAsync(Guid id, string? paymentMethod, CancellationToken ct = default)
    {
        if (userContext.CurrentUserId is not { } staffId)
        {
            return Task.FromResult(ServiceResult.Failure(ErrorCode.AuthUnauthorized));
        }

        if (paymentMethod is not (PaymentMethod.Cash or PaymentMethod.BankTransfer))
        {
            return Task.FromResult(ServiceResult.ValidationError(ErrorCode.CommonInvalidRequest));
        }

        return SettleFineAsync(
            id,
            fine =>
            {
                fine.FineStatus = FineStatus.Paid;
                fine.PaymentMethod = paymentMethod;
                fine.PaidAt = dateTimeProvider.UtcNow;
                fine.HandledBy = staffId;
                return ServiceResult.Success($"Đã thu {CurrencyHelper.ToVnd(fine.Amount)}.");
            },
            ct);
    }

    public Task<ServiceResult> WaiveFineAsync(Guid id, string? note, CancellationToken ct = default)
    {
        if (userContext.CurrentUserId is not { } staffId)
        {
            return Task.FromResult(ServiceResult.Failure(ErrorCode.AuthUnauthorized));
        }

        if (!userContext.IsInRole(AppRoles.Admin))
        {
            return Task.FromResult(ServiceResult.Failure(ErrorCode.AuthAccessDenied));
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            return Task.FromResult(ServiceResult.ValidationError(ErrorCode.FineWaiveNoteRequired));
        }

        return SettleFineAsync(
            id,
            fine =>
            {
                fine.FineStatus = FineStatus.Waived;
                fine.Note = note.Trim();
                fine.HandledBy = staffId;
                return ServiceResult.Success($"Đã miễn khoản phạt {CurrencyHelper.ToVnd(fine.Amount)}.");
            },
            ct);
    }

    private Task<ServiceResult> SettleFineAsync(Guid id, Func<Fine, ServiceResult> apply, CancellationToken ct) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var fine = await unitOfWork.Fines.FirstOrDefaultAsync(
                f => f.Id == id && f.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);

            if (fine is null)
            {
                return ServiceResult.Failure(ErrorCode.FineNotFound);
            }

            if (fine.FineStatus != FineStatus.Unpaid)
            {
                return ServiceResult.Failure(ErrorCode.FineAlreadySettled);
            }

            return apply(fine);
        }, cancellationToken: ct);
}
