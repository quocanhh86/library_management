using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Repositories;

namespace web_mvc_management.Services;

public class ReaderTypeService(IUnitOfWork unitOfWork) : IReaderTypeService
{
    public async Task<IReadOnlyList<ReaderTypeItemViewModel>> GetReaderTypesAsync(CancellationToken ct = default) =>
        await unitOfWork.ReaderTypes.GetQueryable(asNoTracking: true)
            .OrderBy(rt => rt.Status == EntityStatus.Deleted ? 1 : 0)
            .ThenByDescending(rt => rt.MaxBorrowBooks)
            .Select(rt => new ReaderTypeItemViewModel
            {
                Id = rt.Id,
                Code = rt.Code,
                Name = rt.Name,
                MaxBorrowBooks = rt.MaxBorrowBooks,
                LoanDays = rt.LoanDays,
                MaxRenewals = rt.MaxRenewals,
                RenewDays = rt.RenewDays,
                CardValidMonths = rt.CardValidMonths,
                Description = rt.Description,
                Status = rt.Status,
                ReaderCount = rt.Readers.Count(r => r.Status != EntityStatus.Deleted)
            })
            .ToListAsync(ct);

    public Task<ServiceResult> SaveReaderTypeAsync(ReaderTypeFormViewModel model, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var codeTrim = model.Code.Trim().ToUpperInvariant();
            var normalizedStatus = LibraryConstants.NormalizeActiveOrInactive(model.Status);

            var duplicate = await unitOfWork.ReaderTypes.FirstOrDefaultAsync(
                rt => rt.Code.ToUpper() == codeTrim && rt.Id != model.Id,
                asNoTracking: true,
                ct);
            if (duplicate != null)
            {
                var msg = duplicate.Status == EntityStatus.Deleted
                    ? $"Mã loại thẻ \"{duplicate.Code}\" đang nằm trong mục Đã xóa mềm (DELETED). Vui lòng khôi phục hoặc xóa vĩnh viễn loại thẻ cũ trước."
                    : null;
                return ServiceResult.ValidationError(ErrorCode.ReaderTypeCodeAlreadyExists, msg);
            }

            if (model.Id.HasValue && model.Id.Value != Guid.Empty)
            {
                var entity = await unitOfWork.ReaderTypes.FirstOrDefaultAsync(
                    rt => rt.Id == model.Id.Value && rt.Status != EntityStatus.Deleted,
                    asNoTracking: false,
                    ct);
                if (entity == null)
                {
                    return ServiceResult.Failure(ErrorCode.ReaderTypeNotFound);
                }

                entity.Code = codeTrim;
                entity.Name = model.Name.Trim();
                entity.MaxBorrowBooks = model.MaxBorrowBooks;
                entity.LoanDays = model.LoanDays;
                entity.MaxRenewals = model.MaxRenewals;
                entity.RenewDays = model.RenewDays;
                entity.CardValidMonths = model.CardValidMonths;
                entity.Description = model.Description?.Trim();
                entity.Status = normalizedStatus;
                return ServiceResult.Success($"Đã cập nhật quy định loại độc giả \"{entity.Name}\" ({entity.MaxBorrowBooks} cuốn / {entity.LoanDays} ngày).");
            }

            var created = new ReaderType
            {
                Id = Guid.NewGuid(),
                Code = codeTrim,
                Name = model.Name.Trim(),
                MaxBorrowBooks = model.MaxBorrowBooks,
                LoanDays = model.LoanDays,
                MaxRenewals = model.MaxRenewals,
                RenewDays = model.RenewDays,
                CardValidMonths = model.CardValidMonths,
                Description = model.Description?.Trim(),
                Status = normalizedStatus
            };
            await unitOfWork.ReaderTypes.AddAsync(created, ct);
            return ServiceResult.Success($"Đã thêm loại độc giả \"{created.Name}\".");
        }, cancellationToken: ct);

    public Task<ServiceResult> DeleteReaderTypeAsync(Guid id, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var entity = await unitOfWork.ReaderTypes.FirstOrDefaultAsync(
                rt => rt.Id == id && rt.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (entity == null)
            {
                return ServiceResult.Failure(ErrorCode.ReaderTypeNotFound);
            }

            if (await unitOfWork.Readers.ExistsAsync(r => r.ReaderTypeId == id && r.Status != EntityStatus.Deleted, ct))
            {
                return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
            }

            entity.Status = EntityStatus.Deleted;
            return ServiceResult.Success($"Đã xóa mềm loại độc giả \"{entity.Name}\" (chuyển sang DELETED).");
        }, cancellationToken: ct);

    public Task<ServiceResult> RestoreReaderTypeAsync(Guid id, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var entity = await unitOfWork.ReaderTypes.FirstOrDefaultAsync(
                rt => rt.Id == id && rt.Status == EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (entity == null)
            {
                return ServiceResult.Failure(ErrorCode.ReaderTypeNotFound);
            }

            if (await unitOfWork.ReaderTypes.ExistsAsync(rt => rt.Id != id && rt.Code.ToUpper() == entity.Code.ToUpper(), ct))
            {
                return ServiceResult.ValidationError(ErrorCode.ReaderTypeCodeAlreadyExists);
            }

            entity.Status = EntityStatus.Active;
            return ServiceResult.Success($"Đã khôi phục loại độc giả \"{entity.Name}\" về trạng thái hoạt động.");
        }, cancellationToken: ct);

    public Task<ServiceResult> HardDeleteReaderTypeAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        if (!LibraryConstants.IsValidHardDeleteConfirm(confirmCode))
        {
            return Task.FromResult(ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid));
        }

        return unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var entity = await unitOfWork.ReaderTypes.FirstOrDefaultAsync(rt => rt.Id == id, asNoTracking: false, ct);
            if (entity == null)
            {
                return ServiceResult.Failure(ErrorCode.ReaderTypeNotFound);
            }

            if (entity.Status != EntityStatus.Deleted)
            {
                return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
            }

            if (await unitOfWork.Readers.ExistsAsync(r => r.ReaderTypeId == id, ct))
            {
                return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
            }

            var name = entity.Name;
            unitOfWork.ReaderTypes.Delete(entity);
            return ServiceResult.Success($"Đã xóa vĩnh viễn loại độc giả \"{name}\" khỏi cơ sở dữ liệu.");
        }, cancellationToken: ct);
    }
}
