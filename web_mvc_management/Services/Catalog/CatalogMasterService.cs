using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Extensions;
using web_mvc_management.Models.ViewModels.AdminCatalog;
using web_mvc_management.Repositories;

namespace web_mvc_management.Services;

public class CatalogMasterService(IUnitOfWork unitOfWork) : ICatalogMasterService
{
    // =========================================================================
    // 1. THỂ LOẠI SÁCH (CATEGORIES)
    // =========================================================================
    public async Task<PagedResult<CategoryItemViewModel>> GetCategoriesPagedAsync(BaseFilter filter, CancellationToken ct = default)
    {
        var query = ApplyStatusFilter(unitOfWork.Categories.GetQueryable(asNoTracking: true), filter);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(kw) || (x.Description != null && x.Description.ToLower().Contains(kw)));
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new CategoryItemViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Status = x.Status,
                BookCount = x.Books.Count(b => b.Status != EntityStatus.Deleted),
                UpdatedAt = x.UpdatedAt
            })
            .ToPagedResultAsync(filter, ct);
    }

    public Task<ServiceResult> SaveCategoryAsync(CategoryFormViewModel model, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var nameTrim = model.Name.Trim();
            var normalizedStatus = LibraryConstants.NormalizeActiveOrInactive(model.Status);

            var duplicate = await unitOfWork.Categories.FirstOrDefaultAsync(
                x => x.Name.ToLower() == nameTrim.ToLower() && x.Id != model.Id,
                asNoTracking: true,
                ct);
            if (duplicate != null)
            {
                var msg = duplicate.Status == EntityStatus.Deleted
                    ? $"Thể loại \"{duplicate.Name}\" đang nằm trong mục Đã xóa mềm (DELETED). Vui lòng khôi phục hoặc xóa vĩnh viễn bản ghi cũ trước."
                    : null;
                return ServiceResult.ValidationError(ErrorCode.CategoryNameAlreadyExists, msg);
            }

            if (model.Id.HasValue && model.Id.Value != Guid.Empty)
            {
                var entity = await unitOfWork.Categories.FirstOrDefaultAsync(
                    x => x.Id == model.Id.Value && x.Status != EntityStatus.Deleted,
                    asNoTracking: false,
                    ct);
                if (entity == null)
                {
                    return ServiceResult.Failure(ErrorCode.CategoryNotFound);
                }

                entity.Name = nameTrim;
                entity.Description = model.Description?.Trim();
                entity.Status = normalizedStatus;
                return ServiceResult.Success($"Đã cập nhật thể loại \"{entity.Name}\".");
            }

            var created = new Category
            {
                Id = Guid.NewGuid(),
                Name = nameTrim,
                Description = model.Description?.Trim(),
                Status = normalizedStatus
            };
            await unitOfWork.Categories.AddAsync(created, ct);
            return ServiceResult.Success($"Đã thêm thể loại \"{created.Name}\".");
        }, cancellationToken: ct);

    public Task<ServiceResult> DeleteCategoryAsync(Guid id, CancellationToken ct = default) =>
        SoftDeleteMasterAsync(
            unitOfWork.Categories,
            id,
            ErrorCode.CategoryNotFound,
            () => unitOfWork.Books.ExistsAsync(x => x.CategoryId == id && x.Status != EntityStatus.Deleted, ct),
            c => $"Đã xóa mềm thể loại \"{c.Name}\" (Bước 1: Đổi sang DELETED).",
            ct);

    public Task<ServiceResult> RestoreCategoryAsync(Guid id, CancellationToken ct = default) =>
        RestoreMasterAsync(
            unitOfWork.Categories,
            id,
            ErrorCode.CategoryNotFound,
            ErrorCode.CategoryNameAlreadyExists,
            c => unitOfWork.Categories.ExistsAsync(x => x.Id != id && x.Name.ToLower() == c.Name.ToLower(), ct),
            c => $"Đã khôi phục thể loại \"{c.Name}\".",
            ct);

    public Task<ServiceResult> HardDeleteCategoryAsync(Guid id, string? confirmCode, CancellationToken ct = default) =>
        HardDeleteMasterAsync(
            unitOfWork.Categories,
            id,
            confirmCode,
            ErrorCode.CategoryNotFound,
            () => unitOfWork.Books.ExistsAsync(x => x.CategoryId == id, ct),
            c => $"Đã xóa vĩnh viễn thể loại \"{c.Name}\" khỏi cơ sở dữ liệu.",
            ct);

    // =========================================================================
    // 2. TÁC GIẢ (AUTHORS)
    // =========================================================================
    public async Task<PagedResult<AuthorItemViewModel>> GetAuthorsPagedAsync(BaseFilter filter, CancellationToken ct = default)
    {
        var query = ApplyStatusFilter(unitOfWork.Authors.GetQueryable(asNoTracking: true), filter);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.ToLower();
            query = query.Where(x => x.FullName.ToLower().Contains(kw) || (x.Biography != null && x.Biography.ToLower().Contains(kw)));
        }

        return await query
            .OrderBy(x => x.FullName)
            .Select(x => new AuthorItemViewModel
            {
                Id = x.Id,
                FullName = x.FullName,
                Biography = x.Biography,
                Status = x.Status,
                BookCount = x.BookAuthors.Count(ba => ba.Status != EntityStatus.Deleted && ba.Book.Status != EntityStatus.Deleted),
                UpdatedAt = x.UpdatedAt
            })
            .ToPagedResultAsync(filter, ct);
    }

    public Task<ServiceResult> SaveAuthorAsync(AuthorFormViewModel model, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var nameTrim = model.FullName.Trim();
            var normalizedStatus = LibraryConstants.NormalizeActiveOrInactive(model.Status);

            var existingAuthor = await unitOfWork.Authors.FirstOrDefaultAsync(
                x => x.FullName.ToLower() == nameTrim.ToLower() && x.Id != model.Id,
                asNoTracking: true,
                ct);
            if (existingAuthor != null)
            {
                var msg = existingAuthor.Status == EntityStatus.Deleted
                    ? $"Tác giả \"{nameTrim}\" đang nằm trong mục Đã xóa mềm. Vui lòng khôi phục hoặc xóa vĩnh viễn trước."
                    : null;
                return ServiceResult.ValidationError(ErrorCode.AuthorNameAlreadyExists, msg);
            }

            if (model.Id.HasValue && model.Id.Value != Guid.Empty)
            {
                var entity = await unitOfWork.Authors.FirstOrDefaultAsync(
                    x => x.Id == model.Id.Value && x.Status != EntityStatus.Deleted,
                    asNoTracking: false,
                    ct);
                if (entity == null)
                {
                    return ServiceResult.Failure(ErrorCode.AuthorNotFound);
                }

                entity.FullName = nameTrim;
                entity.Biography = model.Biography?.Trim();
                entity.Status = normalizedStatus;
                return ServiceResult.Success($"Đã cập nhật tác giả \"{entity.FullName}\".");
            }

            var created = new Author
            {
                Id = Guid.NewGuid(),
                FullName = nameTrim,
                Biography = model.Biography?.Trim(),
                Status = normalizedStatus
            };
            await unitOfWork.Authors.AddAsync(created, ct);
            return ServiceResult.Success($"Đã thêm tác giả \"{created.FullName}\".");
        }, cancellationToken: ct);

    public Task<ServiceResult> DeleteAuthorAsync(Guid id, CancellationToken ct = default) =>
        SoftDeleteMasterAsync(
            unitOfWork.Authors,
            id,
            ErrorCode.AuthorNotFound,
            () => unitOfWork.BookAuthors.ExistsAsync(x => x.AuthorId == id && x.Status != EntityStatus.Deleted && x.Book.Status != EntityStatus.Deleted, ct),
            a => $"Đã xóa mềm tác giả \"{a.FullName}\" (Bước 1: Đổi sang DELETED).",
            ct);

    public Task<ServiceResult> RestoreAuthorAsync(Guid id, CancellationToken ct = default) =>
        RestoreMasterAsync(
            unitOfWork.Authors,
            id,
            ErrorCode.AuthorNotFound,
            ErrorCode.AuthorNameAlreadyExists,
            a => unitOfWork.Authors.ExistsAsync(x => x.Id != id && x.FullName.ToLower() == a.FullName.ToLower(), ct),
            a => $"Đã khôi phục tác giả \"{a.FullName}\".",
            ct);

    public Task<ServiceResult> HardDeleteAuthorAsync(Guid id, string? confirmCode, CancellationToken ct = default) =>
        HardDeleteMasterAsync(
            unitOfWork.Authors,
            id,
            confirmCode,
            ErrorCode.AuthorNotFound,
            () => unitOfWork.BookAuthors.ExistsAsync(x => x.AuthorId == id, ct),
            a => $"Đã xóa vĩnh viễn tác giả \"{a.FullName}\" khỏi cơ sở dữ liệu.",
            ct);

    // =========================================================================
    // 3. NHÀ XUẤT BẢN (PUBLISHERS)
    // =========================================================================
    public async Task<PagedResult<PublisherItemViewModel>> GetPublishersPagedAsync(BaseFilter filter, CancellationToken ct = default)
    {
        var query = ApplyStatusFilter(unitOfWork.Publishers.GetQueryable(asNoTracking: true), filter);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.ToLower();
            query = query.Where(x =>
                x.Name.ToLower().Contains(kw) ||
                (x.Email != null && x.Email.ToLower().Contains(kw)) ||
                (x.Phone != null && x.Phone.Contains(kw)));
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new PublisherItemViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Address = x.Address,
                Phone = x.Phone,
                Email = x.Email,
                Website = x.Website,
                Status = x.Status,
                BookCount = x.Books.Count(b => b.Status != EntityStatus.Deleted),
                UpdatedAt = x.UpdatedAt
            })
            .ToPagedResultAsync(filter, ct);
    }

    public Task<ServiceResult> SavePublisherAsync(PublisherFormViewModel model, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var nameTrim = model.Name.Trim();
            var normalizedStatus = LibraryConstants.NormalizeActiveOrInactive(model.Status);

            var duplicate = await unitOfWork.Publishers.FirstOrDefaultAsync(
                x => x.Name.ToLower() == nameTrim.ToLower() && x.Id != model.Id,
                asNoTracking: true,
                ct);
            if (duplicate != null)
            {
                var msg = duplicate.Status == EntityStatus.Deleted
                    ? $"Nhà xuất bản \"{duplicate.Name}\" đang nằm trong mục Đã xóa mềm (DELETED). Vui lòng khôi phục hoặc xóa vĩnh viễn bản ghi cũ trước."
                    : null;
                return ServiceResult.ValidationError(ErrorCode.PublisherNameAlreadyExists, msg);
            }

            if (model.Id.HasValue && model.Id.Value != Guid.Empty)
            {
                var entity = await unitOfWork.Publishers.FirstOrDefaultAsync(
                    x => x.Id == model.Id.Value && x.Status != EntityStatus.Deleted,
                    asNoTracking: false,
                    ct);
                if (entity == null)
                {
                    return ServiceResult.Failure(ErrorCode.PublisherNotFound);
                }

                entity.Name = nameTrim;
                entity.Address = model.Address?.Trim();
                entity.Phone = model.Phone?.Trim();
                entity.Email = model.Email?.Trim();
                entity.Website = model.Website?.Trim();
                entity.Status = normalizedStatus;
                return ServiceResult.Success($"Đã cập nhật nhà xuất bản \"{entity.Name}\".");
            }

            var created = new Publisher
            {
                Id = Guid.NewGuid(),
                Name = nameTrim,
                Address = model.Address?.Trim(),
                Phone = model.Phone?.Trim(),
                Email = model.Email?.Trim(),
                Website = model.Website?.Trim(),
                Status = normalizedStatus
            };
            await unitOfWork.Publishers.AddAsync(created, ct);
            return ServiceResult.Success($"Đã thêm nhà xuất bản \"{created.Name}\".");
        }, cancellationToken: ct);

    public Task<ServiceResult> DeletePublisherAsync(Guid id, CancellationToken ct = default) =>
        SoftDeleteMasterAsync(
            unitOfWork.Publishers,
            id,
            ErrorCode.PublisherNotFound,
            () => unitOfWork.Books.ExistsAsync(x => x.PublisherId == id && x.Status != EntityStatus.Deleted, ct),
            p => $"Đã xóa mềm nhà xuất bản \"{p.Name}\" (Bước 1: Đổi sang DELETED).",
            ct);

    public Task<ServiceResult> RestorePublisherAsync(Guid id, CancellationToken ct = default) =>
        RestoreMasterAsync(
            unitOfWork.Publishers,
            id,
            ErrorCode.PublisherNotFound,
            ErrorCode.PublisherNameAlreadyExists,
            p => unitOfWork.Publishers.ExistsAsync(x => x.Id != id && x.Name.ToLower() == p.Name.ToLower(), ct),
            p => $"Đã khôi phục nhà xuất bản \"{p.Name}\".",
            ct);

    public Task<ServiceResult> HardDeletePublisherAsync(Guid id, string? confirmCode, CancellationToken ct = default) =>
        HardDeleteMasterAsync(
            unitOfWork.Publishers,
            id,
            confirmCode,
            ErrorCode.PublisherNotFound,
            () => unitOfWork.Books.ExistsAsync(x => x.PublisherId == id, ct),
            p => $"Đã xóa vĩnh viễn nhà xuất bản \"{p.Name}\" khỏi cơ sở dữ liệu.",
            ct);

    // =========================================================================
    // 4. VỊ TRÍ KỆ SÁCH (SHELVES)
    // =========================================================================
    public async Task<PagedResult<ShelfItemViewModel>> GetShelvesPagedAsync(BaseFilter filter, CancellationToken ct = default)
    {
        var query = ApplyStatusFilter(unitOfWork.Shelves.GetQueryable(asNoTracking: true), filter);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.ToLower();
            query = query.Where(x => x.Code.ToLower().Contains(kw) || (x.Description != null && x.Description.ToLower().Contains(kw)));
        }

        return await query
            .OrderBy(x => x.Code)
            .Select(x => new ShelfItemViewModel
            {
                Id = x.Id,
                Code = x.Code,
                Description = x.Description,
                Status = x.Status,
                CopyCount = x.BookCopies.Count(c => c.Status != EntityStatus.Deleted && c.CopyStatus != LibraryConstants.CopyStatus.Disposed),
                AvailableCopyCount = x.BookCopies.Count(c => c.Status != EntityStatus.Deleted && c.CopyStatus == LibraryConstants.CopyStatus.Available),
                UpdatedAt = x.UpdatedAt
            })
            .ToPagedResultAsync(filter, ct);
    }

    public Task<ServiceResult> SaveShelfAsync(ShelfFormViewModel model, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var codeTrim = model.Code.Trim().ToUpperInvariant();
            var normalizedStatus = LibraryConstants.NormalizeActiveOrInactive(model.Status);

            var duplicate = await unitOfWork.Shelves.FirstOrDefaultAsync(
                x => x.Code.ToUpper() == codeTrim && x.Id != model.Id,
                asNoTracking: true,
                ct);
            if (duplicate != null)
            {
                var msg = duplicate.Status == EntityStatus.Deleted
                    ? $"Mã kệ \"{duplicate.Code}\" đang nằm trong mục Đã xóa mềm (DELETED). Vui lòng khôi phục hoặc xóa vĩnh viễn kệ cũ trước."
                    : null;
                return ServiceResult.ValidationError(ErrorCode.ShelfCodeAlreadyExists, msg);
            }

            if (model.Id.HasValue && model.Id.Value != Guid.Empty)
            {
                var entity = await unitOfWork.Shelves.FirstOrDefaultAsync(
                    x => x.Id == model.Id.Value && x.Status != EntityStatus.Deleted,
                    asNoTracking: false,
                    ct);
                if (entity == null)
                {
                    return ServiceResult.Failure(ErrorCode.ShelfNotFound);
                }

                entity.Code = codeTrim;
                entity.Description = model.Description?.Trim();
                entity.Status = normalizedStatus;
                return ServiceResult.Success($"Đã cập nhật kệ sách \"{entity.Code}\".");
            }

            var created = new Shelf
            {
                Id = Guid.NewGuid(),
                Code = codeTrim,
                Description = model.Description?.Trim(),
                Status = normalizedStatus
            };
            await unitOfWork.Shelves.AddAsync(created, ct);
            return ServiceResult.Success($"Đã thêm vị trí kệ \"{created.Code}\".");
        }, cancellationToken: ct);

    public Task<ServiceResult> DeleteShelfAsync(Guid id, CancellationToken ct = default) =>
        SoftDeleteMasterAsync(
            unitOfWork.Shelves,
            id,
            ErrorCode.ShelfNotFound,
            () => unitOfWork.BookCopies.ExistsAsync(x => x.ShelfId == id && x.Status != EntityStatus.Deleted && x.CopyStatus != LibraryConstants.CopyStatus.Disposed, ct),
            s => $"Đã xóa mềm vị trí kệ \"{s.Code}\" (Bước 1: Đổi sang DELETED).",
            ct);

    public Task<ServiceResult> RestoreShelfAsync(Guid id, CancellationToken ct = default) =>
        RestoreMasterAsync(
            unitOfWork.Shelves,
            id,
            ErrorCode.ShelfNotFound,
            ErrorCode.ShelfCodeAlreadyExists,
            s => unitOfWork.Shelves.ExistsAsync(x => x.Id != id && x.Code.ToUpper() == s.Code.ToUpper(), ct),
            s => $"Đã khôi phục vị trí kệ \"{s.Code}\".",
            ct);

    public Task<ServiceResult> HardDeleteShelfAsync(Guid id, string? confirmCode, CancellationToken ct = default) =>
        HardDeleteMasterAsync(
            unitOfWork.Shelves,
            id,
            confirmCode,
            ErrorCode.ShelfNotFound,
            () => unitOfWork.BookCopies.ExistsAsync(x => x.ShelfId == id, ct),
            s => $"Đã xóa vĩnh viễn vị trí kệ \"{s.Code}\" khỏi cơ sở dữ liệu.",
            ct);

    // =========================================================================
    // HELPERS DÙNG CHUNG (DRY CHO 4 DANH MỤC GỐC)
    // =========================================================================
    private static IQueryable<TEntity> ApplyStatusFilter<TEntity>(IQueryable<TEntity> query, BaseFilter filter)
        where TEntity : BaseEntity =>
        !string.IsNullOrWhiteSpace(filter.Status)
            ? query.Where(x => x.Status == filter.Status)
            : query.Where(x => x.Status != EntityStatus.Deleted);

    private Task<ServiceResult> SoftDeleteMasterAsync<TEntity>(
        IBaseRepository<TEntity> repository,
        Guid id,
        ErrorCode notFoundCode,
        Func<Task<bool>> isInUseAsync,
        Func<TEntity, string> successMessage,
        CancellationToken ct)
        where TEntity : BaseEntity =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var entity = await repository.FirstOrDefaultAsync(x => x.Id == id && x.Status != EntityStatus.Deleted, asNoTracking: false, ct);
            if (entity == null)
            {
                return ServiceResult.Failure(notFoundCode);
            }

            if (await isInUseAsync())
            {
                return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
            }

            entity.Status = EntityStatus.Deleted;
            return ServiceResult.Success(successMessage(entity));
        }, cancellationToken: ct);

    private Task<ServiceResult> RestoreMasterAsync<TEntity>(
        IBaseRepository<TEntity> repository,
        Guid id,
        ErrorCode notFoundCode,
        ErrorCode duplicateCode,
        Func<TEntity, Task<bool>> isDuplicateAsync,
        Func<TEntity, string> successMessage,
        CancellationToken ct)
        where TEntity : BaseEntity =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var entity = await repository.FirstOrDefaultAsync(x => x.Id == id && x.Status == EntityStatus.Deleted, asNoTracking: false, ct);
            if (entity == null)
            {
                return ServiceResult.Failure(notFoundCode);
            }

            if (await isDuplicateAsync(entity))
            {
                return ServiceResult.ValidationError(duplicateCode);
            }

            entity.Status = EntityStatus.Active;
            return ServiceResult.Success(successMessage(entity));
        }, cancellationToken: ct);

    private Task<ServiceResult> HardDeleteMasterAsync<TEntity>(
        IBaseRepository<TEntity> repository,
        Guid id,
        string? confirmCode,
        ErrorCode notFoundCode,
        Func<Task<bool>> hasAnyReferenceAsync,
        Func<TEntity, string> successMessage,
        CancellationToken ct)
        where TEntity : BaseEntity
    {
        if (!LibraryConstants.IsValidHardDeleteConfirm(confirmCode))
        {
            return Task.FromResult(ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid));
        }

        return unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var entity = await repository.FirstOrDefaultAsync(x => x.Id == id, asNoTracking: false, ct);
            if (entity == null)
            {
                return ServiceResult.Failure(notFoundCode);
            }

            if (entity.Status != EntityStatus.Deleted)
            {
                return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
            }

            if (await hasAnyReferenceAsync())
            {
                return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
            }

            var message = successMessage(entity);
            repository.Delete(entity);
            return ServiceResult.Success(message);
        }, cancellationToken: ct);
    }
}
