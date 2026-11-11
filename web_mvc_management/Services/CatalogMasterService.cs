using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Extensions;
using web_mvc_management.Models.ViewModels.AdminCatalog;
using web_mvc_management.Models.ViewModels.Catalog;
using web_mvc_management.Repositories;

namespace web_mvc_management.Services;

public class CatalogMasterService(IUnitOfWork unitOfWork) : ICatalogMasterService
{
    private static bool IsValidHardDeleteConfirm(string? confirmCode) =>
        string.Equals(confirmCode?.Trim(), "XOA", StringComparison.OrdinalIgnoreCase);

    // =========================================================================
    // SELECT OPTIONS CHO DROPDOWN PHÂN LOẠI SÁCH
    // =========================================================================
    public async Task<IReadOnlyList<SelectOptionViewModel>> GetActiveCategoriesAsync(CancellationToken ct = default)
    {
        return await unitOfWork.Categories.GetQueryable(asNoTracking: true)
            .Where(x => x.Status == EntityStatus.Active)
            .OrderBy(x => x.Name)
            .Select(x => new SelectOptionViewModel(x.Id, x.Name))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SelectOptionViewModel>> GetActiveAuthorsAsync(CancellationToken ct = default)
    {
        return await unitOfWork.Authors.GetQueryable(asNoTracking: true)
            .Where(x => x.Status == EntityStatus.Active)
            .OrderBy(x => x.FullName)
            .Select(x => new SelectOptionViewModel(x.Id, x.FullName))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SelectOptionViewModel>> GetActivePublishersAsync(CancellationToken ct = default)
    {
        return await unitOfWork.Publishers.GetQueryable(asNoTracking: true)
            .Where(x => x.Status == EntityStatus.Active)
            .OrderBy(x => x.Name)
            .Select(x => new SelectOptionViewModel(x.Id, x.Name))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SelectOptionViewModel>> GetActiveShelvesAsync(CancellationToken ct = default)
    {
        return await unitOfWork.Shelves.GetQueryable(asNoTracking: true)
            .Where(x => x.Status == EntityStatus.Active)
            .OrderBy(x => x.Code)
            .Select(x => new SelectOptionViewModel(
                x.Id,
                string.IsNullOrWhiteSpace(x.Description) ? x.Code : $"{x.Code} – {x.Description}"))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SelectOptionViewModel>> GetActiveBooksSelectAsync(CancellationToken ct = default)
    {
        return await unitOfWork.Books.GetQueryable(asNoTracking: true)
            .Where(x => x.Status == EntityStatus.Active)
            .OrderBy(x => x.Title)
            .Select(x => new SelectOptionViewModel(
                x.Id,
                string.IsNullOrWhiteSpace(x.Isbn) ? x.Title : $"{x.Title} ({x.Isbn})"))
            .ToListAsync(ct);
    }

    // =========================================================================
    // 1. THỂ LOẠI SÁCH (CATEGORIES)
    // =========================================================================
    public async Task<PagedResult<CategoryItemViewModel>> GetCategoriesPagedAsync(BaseFilter filter, CancellationToken ct = default)
    {
        var query = unitOfWork.Categories.GetQueryable(asNoTracking: true);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(x => x.Status == filter.Status);
        }
        else
        {
            query = query.Where(x => x.Status != EntityStatus.Deleted);
        }

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

    public async Task<ServiceResult> SaveCategoryAsync(CategoryFormViewModel model, CancellationToken ct = default)
    {
        var nameTrim = model.Name.Trim();

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var duplicateEntity = await unitOfWork.Categories.FirstOrDefaultAsync(
                x => x.Name.ToLower() == nameTrim.ToLower() && x.Id != model.Id,
                asNoTracking: true,
                ct);

            if (duplicateEntity != null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                var msg = duplicateEntity.Status == EntityStatus.Deleted
                    ? $"Thể loại \"{duplicateEntity.Name}\" đang nằm trong mục Đã xóa mềm (DELETED). Vui lòng khôi phục hoặc xóa vĩnh viễn bản ghi cũ trước."
                    : null;
                return ServiceResult.ValidationError(ErrorCode.CategoryNameAlreadyExists, msg);
            }

            if (model.Id.HasValue && model.Id.Value != Guid.Empty)
            {
                var entity = await unitOfWork.Categories.FirstOrDefaultAsync(x => x.Id == model.Id.Value, asNoTracking: false, ct);
                if (entity == null)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult.Failure(ErrorCode.CategoryNotFound);
                }

                entity.Name = nameTrim;
                entity.Description = model.Description?.Trim();
                entity.Status = model.Status;
                await unitOfWork.CommitTransactionAsync(ct);
                return ServiceResult.Success($"Đã cập nhật thể loại \"{entity.Name}\".");
            }
            else
            {
                var entity = new Category
                {
                    Id = Guid.NewGuid(),
                    Name = nameTrim,
                    Description = model.Description?.Trim(),
                    Status = model.Status
                };
                await unitOfWork.Categories.AddAsync(entity, ct);
                await unitOfWork.CommitTransactionAsync(ct);
                return ServiceResult.Success($"Đã thêm thể loại \"{entity.Name}\".");
            }
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var entity = await unitOfWork.Categories.FirstOrDefaultAsync(x => x.Id == id && x.Status != EntityStatus.Deleted, asNoTracking: false, ct);
            if (entity == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.CategoryNotFound);
            }

            var hasBooks = await unitOfWork.Books.ExistsAsync(x => x.CategoryId == id && x.Status != EntityStatus.Deleted, ct);
            if (hasBooks)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
            }

            entity.Status = EntityStatus.Deleted;
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã xóa mềm thể loại \"{entity.Name}\" (Bước 1: Đổi sang DELETED).");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> RestoreCategoryAsync(Guid id, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var entity = await unitOfWork.Categories.FirstOrDefaultAsync(
                x => x.Id == id && x.Status == EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (entity == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.CategoryNotFound);
            }

            var duplicate = await unitOfWork.Categories.ExistsAsync(
                x => x.Id != id && x.Name.ToLower() == entity.Name.ToLower(),
                ct);
            if (duplicate)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.ValidationError(ErrorCode.CategoryNameAlreadyExists);
            }

            entity.Status = EntityStatus.Active;
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã khôi phục thể loại \"{entity.Name}\" về trạng thái hoạt động.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> HardDeleteCategoryAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        if (!IsValidHardDeleteConfirm(confirmCode))
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid);
        }

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var entity = await unitOfWork.Categories.FirstOrDefaultAsync(x => x.Id == id, asNoTracking: false, ct);
            if (entity == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.CategoryNotFound);
            }

            if (entity.Status != EntityStatus.Deleted)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
            }

            var hasAnyBookRef = await unitOfWork.Books.ExistsAsync(x => x.CategoryId == id, ct);
            if (hasAnyBookRef)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
            }

            var name = entity.Name;
            unitOfWork.Categories.Delete(entity);
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã xóa vĩnh viễn thể loại \"{name}\" khỏi cơ sở dữ liệu.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // =========================================================================
    // 2. TÁC GIẢ (AUTHORS)
    // =========================================================================
    public async Task<PagedResult<AuthorItemViewModel>> GetAuthorsPagedAsync(BaseFilter filter, CancellationToken ct = default)
    {
        var query = unitOfWork.Authors.GetQueryable(asNoTracking: true);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(x => x.Status == filter.Status);
        }
        else
        {
            query = query.Where(x => x.Status != EntityStatus.Deleted);
        }

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

    public async Task<ServiceResult> SaveAuthorAsync(AuthorFormViewModel model, CancellationToken ct = default)
    {
        var nameTrim = model.FullName.Trim();
        var normalizedStatus = model.Status == EntityStatus.Inactive ? EntityStatus.Inactive : EntityStatus.Active;

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var existingAuthor = await unitOfWork.Authors.FirstOrDefaultAsync(
                x => x.FullName.ToLower() == nameTrim.ToLower() && x.Id != model.Id,
                asNoTracking: true,
                ct);

            if (existingAuthor != null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                var msg = existingAuthor.Status == EntityStatus.Deleted
                    ? $"Tác giả \"{nameTrim}\" đang nằm trong mục Đã xóa mềm. Vui lòng khôi phục hoặc xóa vĩnh viễn trước."
                    : null;
                return ServiceResult.ValidationError(ErrorCode.AuthorNameAlreadyExists, msg);
            }

            if (model.Id.HasValue && model.Id.Value != Guid.Empty)
            {
                var entity = await unitOfWork.Authors.FirstOrDefaultAsync(x => x.Id == model.Id.Value, asNoTracking: false, ct);
                if (entity == null)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult.Failure(ErrorCode.AuthorNotFound);
                }

                entity.FullName = nameTrim;
                entity.Biography = model.Biography?.Trim();
                entity.Status = normalizedStatus;
                await unitOfWork.CommitTransactionAsync(ct);
                return ServiceResult.Success($"Đã cập nhật tác giả \"{entity.FullName}\".");
            }
            else
            {
                var entity = new Author
                {
                    Id = Guid.NewGuid(),
                    FullName = nameTrim,
                    Biography = model.Biography?.Trim(),
                    Status = normalizedStatus
                };
                await unitOfWork.Authors.AddAsync(entity, ct);
                await unitOfWork.CommitTransactionAsync(ct);
                return ServiceResult.Success($"Đã thêm tác giả \"{entity.FullName}\".");
            }
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> DeleteAuthorAsync(Guid id, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var entity = await unitOfWork.Authors.FirstOrDefaultAsync(x => x.Id == id && x.Status != EntityStatus.Deleted, asNoTracking: false, ct);
            if (entity == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.AuthorNotFound);
            }

            var hasBooks = await unitOfWork.BookAuthors.ExistsAsync(
                x => x.AuthorId == id && x.Status != EntityStatus.Deleted && x.Book.Status != EntityStatus.Deleted,
                ct);

            if (hasBooks)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
            }

            entity.Status = EntityStatus.Deleted;
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã xóa mềm tác giả \"{entity.FullName}\" (Bước 1: Đổi sang DELETED).");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> RestoreAuthorAsync(Guid id, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var entity = await unitOfWork.Authors.FirstOrDefaultAsync(
                x => x.Id == id && x.Status == EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (entity == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.AuthorNotFound);
            }

            var duplicateActive = await unitOfWork.Authors.ExistsAsync(
                x => x.Id != id && x.Status != EntityStatus.Deleted && x.FullName.ToLower() == entity.FullName.ToLower(),
                ct);
            if (duplicateActive)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.ValidationError(ErrorCode.AuthorNameAlreadyExists);
            }

            entity.Status = EntityStatus.Active;
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã khôi phục tác giả \"{entity.FullName}\".");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> HardDeleteAuthorAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        if (!IsValidHardDeleteConfirm(confirmCode))
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid);
        }

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var entity = await unitOfWork.Authors.FirstOrDefaultAsync(x => x.Id == id, asNoTracking: false, ct);
            if (entity == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.AuthorNotFound);
            }

            if (entity.Status != EntityStatus.Deleted)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
            }

            var hasAnyLink = await unitOfWork.BookAuthors.ExistsAsync(x => x.AuthorId == id, ct);
            if (hasAnyLink)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
            }

            var fullName = entity.FullName;
            unitOfWork.Authors.Delete(entity);
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã xóa vĩnh viễn tác giả \"{fullName}\" khỏi cơ sở dữ liệu.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // =========================================================================
    // 3. NHÀ XUẤT BẢN (PUBLISHERS)
    // =========================================================================
    public async Task<PagedResult<PublisherItemViewModel>> GetPublishersPagedAsync(BaseFilter filter, CancellationToken ct = default)
    {
        var query = unitOfWork.Publishers.GetQueryable(asNoTracking: true);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(x => x.Status == filter.Status);
        }
        else
        {
            query = query.Where(x => x.Status != EntityStatus.Deleted);
        }

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

    public async Task<ServiceResult> SavePublisherAsync(PublisherFormViewModel model, CancellationToken ct = default)
    {
        var nameTrim = model.Name.Trim();

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var duplicateEntity = await unitOfWork.Publishers.FirstOrDefaultAsync(
                x => x.Name.ToLower() == nameTrim.ToLower() && x.Id != model.Id,
                asNoTracking: true,
                ct);

            if (duplicateEntity != null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                var msg = duplicateEntity.Status == EntityStatus.Deleted
                    ? $"Nhà xuất bản \"{duplicateEntity.Name}\" đang nằm trong mục Đã xóa mềm (DELETED). Vui lòng khôi phục hoặc xóa vĩnh viễn bản ghi cũ trước."
                    : null;
                return ServiceResult.ValidationError(ErrorCode.PublisherNameAlreadyExists, msg);
            }

            if (model.Id.HasValue && model.Id.Value != Guid.Empty)
            {
                var entity = await unitOfWork.Publishers.FirstOrDefaultAsync(x => x.Id == model.Id.Value, asNoTracking: false, ct);
                if (entity == null)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult.Failure(ErrorCode.PublisherNotFound);
                }

                entity.Name = nameTrim;
                entity.Address = model.Address?.Trim();
                entity.Phone = model.Phone?.Trim();
                entity.Email = model.Email?.Trim();
                entity.Website = model.Website?.Trim();
                entity.Status = model.Status;
                await unitOfWork.CommitTransactionAsync(ct);
                return ServiceResult.Success($"Đã cập nhật nhà xuất bản \"{entity.Name}\".");
            }
            else
            {
                var entity = new Publisher
                {
                    Id = Guid.NewGuid(),
                    Name = nameTrim,
                    Address = model.Address?.Trim(),
                    Phone = model.Phone?.Trim(),
                    Email = model.Email?.Trim(),
                    Website = model.Website?.Trim(),
                    Status = model.Status
                };
                await unitOfWork.Publishers.AddAsync(entity, ct);
                await unitOfWork.CommitTransactionAsync(ct);
                return ServiceResult.Success($"Đã thêm nhà xuất bản \"{entity.Name}\".");
            }
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> DeletePublisherAsync(Guid id, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var entity = await unitOfWork.Publishers.FirstOrDefaultAsync(x => x.Id == id && x.Status != EntityStatus.Deleted, asNoTracking: false, ct);
            if (entity == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.PublisherNotFound);
            }

            var hasBooks = await unitOfWork.Books.ExistsAsync(x => x.PublisherId == id && x.Status != EntityStatus.Deleted, ct);
            if (hasBooks)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
            }

            entity.Status = EntityStatus.Deleted;
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã xóa mềm nhà xuất bản \"{entity.Name}\" (Bước 1: Đổi sang DELETED).");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> RestorePublisherAsync(Guid id, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var entity = await unitOfWork.Publishers.FirstOrDefaultAsync(
                x => x.Id == id && x.Status == EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (entity == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.PublisherNotFound);
            }

            var duplicate = await unitOfWork.Publishers.ExistsAsync(
                x => x.Id != id && x.Name.ToLower() == entity.Name.ToLower(),
                ct);
            if (duplicate)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.ValidationError(ErrorCode.PublisherNameAlreadyExists);
            }

            entity.Status = EntityStatus.Active;
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã khôi phục nhà xuất bản \"{entity.Name}\".");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> HardDeletePublisherAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        if (!IsValidHardDeleteConfirm(confirmCode))
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid);
        }

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var entity = await unitOfWork.Publishers.FirstOrDefaultAsync(x => x.Id == id, asNoTracking: false, ct);
            if (entity == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.PublisherNotFound);
            }

            if (entity.Status != EntityStatus.Deleted)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
            }

            var hasAnyBook = await unitOfWork.Books.ExistsAsync(x => x.PublisherId == id, ct);
            if (hasAnyBook)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
            }

            var name = entity.Name;
            unitOfWork.Publishers.Delete(entity);
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã xóa vĩnh viễn nhà xuất bản \"{name}\" khỏi cơ sở dữ liệu.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // =========================================================================
    // 4. VỊ TRÍ KỆ SÁCH (SHELVES)
    // =========================================================================
    public async Task<PagedResult<ShelfItemViewModel>> GetShelvesPagedAsync(BaseFilter filter, CancellationToken ct = default)
    {
        var query = unitOfWork.Shelves.GetQueryable(asNoTracking: true);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(x => x.Status == filter.Status);
        }
        else
        {
            query = query.Where(x => x.Status != EntityStatus.Deleted);
        }

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

    public async Task<ServiceResult> SaveShelfAsync(ShelfFormViewModel model, CancellationToken ct = default)
    {
        var codeTrim = model.Code.Trim().ToUpperInvariant();

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var duplicateEntity = await unitOfWork.Shelves.FirstOrDefaultAsync(
                x => x.Code.ToUpper() == codeTrim && x.Id != model.Id,
                asNoTracking: true,
                ct);

            if (duplicateEntity != null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                var msg = duplicateEntity.Status == EntityStatus.Deleted
                    ? $"Mã kệ \"{duplicateEntity.Code}\" đang nằm trong mục Đã xóa mềm (DELETED). Vui lòng khôi phục hoặc xóa vĩnh viễn kệ cũ trước."
                    : null;
                return ServiceResult.ValidationError(ErrorCode.ShelfCodeAlreadyExists, msg);
            }

            if (model.Id.HasValue && model.Id.Value != Guid.Empty)
            {
                var entity = await unitOfWork.Shelves.FirstOrDefaultAsync(x => x.Id == model.Id.Value, asNoTracking: false, ct);
                if (entity == null)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult.Failure(ErrorCode.ShelfNotFound);
                }

                entity.Code = codeTrim;
                entity.Description = model.Description?.Trim();
                entity.Status = model.Status;
                await unitOfWork.CommitTransactionAsync(ct);
                return ServiceResult.Success($"Đã cập nhật kệ sách \"{entity.Code}\".");
            }
            else
            {
                var entity = new Shelf
                {
                    Id = Guid.NewGuid(),
                    Code = codeTrim,
                    Description = model.Description?.Trim(),
                    Status = model.Status
                };
                await unitOfWork.Shelves.AddAsync(entity, ct);
                await unitOfWork.CommitTransactionAsync(ct);
                return ServiceResult.Success($"Đã thêm vị trí kệ \"{entity.Code}\".");
            }
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> DeleteShelfAsync(Guid id, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var entity = await unitOfWork.Shelves.FirstOrDefaultAsync(x => x.Id == id && x.Status != EntityStatus.Deleted, asNoTracking: false, ct);
            if (entity == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ShelfNotFound);
            }

            var hasCopies = await unitOfWork.BookCopies.ExistsAsync(
                x => x.ShelfId == id && x.Status != EntityStatus.Deleted && x.CopyStatus != LibraryConstants.CopyStatus.Disposed,
                ct);

            if (hasCopies)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
            }

            entity.Status = EntityStatus.Deleted;
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã xóa mềm vị trí kệ \"{entity.Code}\" (Bước 1: Đổi sang DELETED).");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> RestoreShelfAsync(Guid id, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var entity = await unitOfWork.Shelves.FirstOrDefaultAsync(
                x => x.Id == id && x.Status == EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (entity == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ShelfNotFound);
            }

            var duplicate = await unitOfWork.Shelves.ExistsAsync(
                x => x.Id != id && x.Code.ToUpper() == entity.Code.ToUpper(),
                ct);
            if (duplicate)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.ValidationError(ErrorCode.ShelfCodeAlreadyExists);
            }

            entity.Status = EntityStatus.Active;
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã khôi phục vị trí kệ \"{entity.Code}\".");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> HardDeleteShelfAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        if (!IsValidHardDeleteConfirm(confirmCode))
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid);
        }

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var entity = await unitOfWork.Shelves.FirstOrDefaultAsync(x => x.Id == id, asNoTracking: false, ct);
            if (entity == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.ShelfNotFound);
            }

            if (entity.Status != EntityStatus.Deleted)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
            }

            var hasAnyCopy = await unitOfWork.BookCopies.ExistsAsync(x => x.ShelfId == id, ct);
            if (hasAnyCopy)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
            }

            var code = entity.Code;
            unitOfWork.Shelves.Delete(entity);
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã xóa vĩnh viễn vị trí kệ \"{code}\" khỏi cơ sở dữ liệu.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }
}
