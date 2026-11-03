using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        var duplicate = await unitOfWork.Categories.ExistsAsync(
            x => x.Status != EntityStatus.Deleted && x.Name.ToLower() == nameTrim.ToLower() && x.Id != model.Id,
            ct);

        if (duplicate)
        {
            return ServiceResult.ValidationError(ErrorCode.CategoryNameAlreadyExists);
        }

        if (model.Id.HasValue && model.Id.Value != Guid.Empty)
        {
            var entity = await unitOfWork.Categories.FirstOrDefaultAsync(x => x.Id == model.Id.Value, asNoTracking: false, ct);
            if (entity == null)
            {
                return ServiceResult.Failure(ErrorCode.CategoryNotFound);
            }

            entity.Name = nameTrim;
            entity.Description = model.Description?.Trim();
            entity.Status = model.Status;
            unitOfWork.Categories.Update(entity);
            await unitOfWork.SaveChangesAsync(ct);
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
            await unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success($"Đã thêm thể loại \"{entity.Name}\".");
        }
    }

    public async Task<ServiceResult> DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await unitOfWork.Categories.FirstOrDefaultAsync(x => x.Id == id && x.Status != EntityStatus.Deleted, asNoTracking: false, ct);
        if (entity == null)
        {
            return ServiceResult.Failure(ErrorCode.CategoryNotFound);
        }

        var hasBooks = await unitOfWork.Books.ExistsAsync(x => x.CategoryId == id && x.Status != EntityStatus.Deleted, ct);
        if (hasBooks)
        {
            return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
        }

        entity.Status = EntityStatus.Deleted;
        unitOfWork.Categories.Update(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã xóa mềm thể loại \"{entity.Name}\" (Bước 1: Đổi sang DELETED).");
    }

    public async Task<ServiceResult> RestoreCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await unitOfWork.Categories.FirstOrDefaultAsync(x => x.Id == id, asNoTracking: false, ct);
        if (entity == null)
        {
            return ServiceResult.Failure(ErrorCode.CategoryNotFound);
        }

        entity.Status = EntityStatus.Active;
        unitOfWork.Categories.Update(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã khôi phục thể loại \"{entity.Name}\" về trạng thái hoạt động.");
    }

    public async Task<ServiceResult> HardDeleteCategoryAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        var entity = await unitOfWork.Categories.FirstOrDefaultAsync(x => x.Id == id, asNoTracking: false, ct);
        if (entity == null)
        {
            return ServiceResult.Failure(ErrorCode.CategoryNotFound);
        }

        if (entity.Status != EntityStatus.Deleted)
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
        }

        if (!IsValidHardDeleteConfirm(confirmCode))
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid);
        }

        var hasAnyBookRef = await unitOfWork.Books.ExistsAsync(x => x.CategoryId == id, ct);
        if (hasAnyBookRef)
        {
            return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
        }

        unitOfWork.Categories.Delete(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã xóa vĩnh viễn thể loại \"{entity.Name}\" khỏi cơ sở dữ liệu.");
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
        var duplicate = await unitOfWork.Authors.ExistsAsync(
            x => x.Status != EntityStatus.Deleted && x.FullName.ToLower() == nameTrim.ToLower() && x.Id != model.Id,
            ct);

        if (duplicate)
        {
            return ServiceResult.ValidationError(ErrorCode.AuthorNameAlreadyExists);
        }

        if (model.Id.HasValue && model.Id.Value != Guid.Empty)
        {
            var entity = await unitOfWork.Authors.FirstOrDefaultAsync(x => x.Id == model.Id.Value, asNoTracking: false, ct);
            if (entity == null)
            {
                return ServiceResult.Failure(ErrorCode.AuthorNotFound);
            }

            entity.FullName = nameTrim;
            entity.Biography = model.Biography?.Trim();
            entity.Status = model.Status;
            unitOfWork.Authors.Update(entity);
            await unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success($"Đã cập nhật tác giả \"{entity.FullName}\".");
        }
        else
        {
            var entity = new Author
            {
                Id = Guid.NewGuid(),
                FullName = nameTrim,
                Biography = model.Biography?.Trim(),
                Status = model.Status
            };
            await unitOfWork.Authors.AddAsync(entity, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success($"Đã thêm tác giả \"{entity.FullName}\".");
        }
    }

    public async Task<ServiceResult> DeleteAuthorAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await unitOfWork.Authors.FirstOrDefaultAsync(x => x.Id == id && x.Status != EntityStatus.Deleted, asNoTracking: false, ct);
        if (entity == null)
        {
            return ServiceResult.Failure(ErrorCode.AuthorNotFound);
        }

        var hasBooks = await unitOfWork.BookAuthors.ExistsAsync(
            x => x.AuthorId == id && x.Status != EntityStatus.Deleted && x.Book.Status != EntityStatus.Deleted,
            ct);

        if (hasBooks)
        {
            return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
        }

        entity.Status = EntityStatus.Deleted;
        unitOfWork.Authors.Update(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã xóa mềm tác giả \"{entity.FullName}\" (Bước 1: Đổi sang DELETED).");
    }

    public async Task<ServiceResult> RestoreAuthorAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await unitOfWork.Authors.FirstOrDefaultAsync(x => x.Id == id, asNoTracking: false, ct);
        if (entity == null)
        {
            return ServiceResult.Failure(ErrorCode.AuthorNotFound);
        }

        entity.Status = EntityStatus.Active;
        unitOfWork.Authors.Update(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã khôi phục tác giả \"{entity.FullName}\".");
    }

    public async Task<ServiceResult> HardDeleteAuthorAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        var entity = await unitOfWork.Authors.FirstOrDefaultAsync(x => x.Id == id, asNoTracking: false, ct);
        if (entity == null)
        {
            return ServiceResult.Failure(ErrorCode.AuthorNotFound);
        }

        if (entity.Status != EntityStatus.Deleted)
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
        }

        if (!IsValidHardDeleteConfirm(confirmCode))
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid);
        }

        var hasAnyLink = await unitOfWork.BookAuthors.ExistsAsync(x => x.AuthorId == id, ct);
        if (hasAnyLink)
        {
            return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
        }

        unitOfWork.Authors.Delete(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã xóa vĩnh viễn tác giả \"{entity.FullName}\" khỏi cơ sở dữ liệu.");
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
        var duplicate = await unitOfWork.Publishers.ExistsAsync(
            x => x.Status != EntityStatus.Deleted && x.Name.ToLower() == nameTrim.ToLower() && x.Id != model.Id,
            ct);

        if (duplicate)
        {
            return ServiceResult.ValidationError(ErrorCode.PublisherNameAlreadyExists);
        }

        if (model.Id.HasValue && model.Id.Value != Guid.Empty)
        {
            var entity = await unitOfWork.Publishers.FirstOrDefaultAsync(x => x.Id == model.Id.Value, asNoTracking: false, ct);
            if (entity == null)
            {
                return ServiceResult.Failure(ErrorCode.PublisherNotFound);
            }

            entity.Name = nameTrim;
            entity.Address = model.Address?.Trim();
            entity.Phone = model.Phone?.Trim();
            entity.Email = model.Email?.Trim();
            entity.Website = model.Website?.Trim();
            entity.Status = model.Status;
            unitOfWork.Publishers.Update(entity);
            await unitOfWork.SaveChangesAsync(ct);
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
            await unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success($"Đã thêm nhà xuất bản \"{entity.Name}\".");
        }
    }

    public async Task<ServiceResult> DeletePublisherAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await unitOfWork.Publishers.FirstOrDefaultAsync(x => x.Id == id && x.Status != EntityStatus.Deleted, asNoTracking: false, ct);
        if (entity == null)
        {
            return ServiceResult.Failure(ErrorCode.PublisherNotFound);
        }

        var hasBooks = await unitOfWork.Books.ExistsAsync(x => x.PublisherId == id && x.Status != EntityStatus.Deleted, ct);
        if (hasBooks)
        {
            return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
        }

        entity.Status = EntityStatus.Deleted;
        unitOfWork.Publishers.Update(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã xóa mềm nhà xuất bản \"{entity.Name}\" (Bước 1: Đổi sang DELETED).");
    }

    public async Task<ServiceResult> RestorePublisherAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await unitOfWork.Publishers.FirstOrDefaultAsync(x => x.Id == id, asNoTracking: false, ct);
        if (entity == null)
        {
            return ServiceResult.Failure(ErrorCode.PublisherNotFound);
        }

        entity.Status = EntityStatus.Active;
        unitOfWork.Publishers.Update(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã khôi phục nhà xuất bản \"{entity.Name}\".");
    }

    public async Task<ServiceResult> HardDeletePublisherAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        var entity = await unitOfWork.Publishers.FirstOrDefaultAsync(x => x.Id == id, asNoTracking: false, ct);
        if (entity == null)
        {
            return ServiceResult.Failure(ErrorCode.PublisherNotFound);
        }

        if (entity.Status != EntityStatus.Deleted)
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
        }

        if (!IsValidHardDeleteConfirm(confirmCode))
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid);
        }

        var hasAnyBook = await unitOfWork.Books.ExistsAsync(x => x.PublisherId == id, ct);
        if (hasAnyBook)
        {
            return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
        }

        unitOfWork.Publishers.Delete(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã xóa vĩnh viễn nhà xuất bản \"{entity.Name}\" khỏi cơ sở dữ liệu.");
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
        var duplicate = await unitOfWork.Shelves.ExistsAsync(
            x => x.Code.ToUpper() == codeTrim && x.Id != model.Id,
            ct);

        if (duplicate)
        {
            return ServiceResult.ValidationError(ErrorCode.ShelfCodeAlreadyExists);
        }

        if (model.Id.HasValue && model.Id.Value != Guid.Empty)
        {
            var entity = await unitOfWork.Shelves.FirstOrDefaultAsync(x => x.Id == model.Id.Value, asNoTracking: false, ct);
            if (entity == null)
            {
                return ServiceResult.Failure(ErrorCode.ShelfNotFound);
            }

            entity.Code = codeTrim;
            entity.Description = model.Description?.Trim();
            entity.Status = model.Status;
            unitOfWork.Shelves.Update(entity);
            await unitOfWork.SaveChangesAsync(ct);
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
            await unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success($"Đã thêm vị trí kệ \"{entity.Code}\".");
        }
    }

    public async Task<ServiceResult> DeleteShelfAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await unitOfWork.Shelves.FirstOrDefaultAsync(x => x.Id == id && x.Status != EntityStatus.Deleted, asNoTracking: false, ct);
        if (entity == null)
        {
            return ServiceResult.Failure(ErrorCode.ShelfNotFound);
        }

        var hasCopies = await unitOfWork.BookCopies.ExistsAsync(
            x => x.ShelfId == id && x.Status != EntityStatus.Deleted && x.CopyStatus != LibraryConstants.CopyStatus.Disposed,
            ct);

        if (hasCopies)
        {
            return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
        }

        entity.Status = EntityStatus.Deleted;
        unitOfWork.Shelves.Update(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã xóa mềm vị trí kệ \"{entity.Code}\" (Bước 1: Đổi sang DELETED).");
    }

    public async Task<ServiceResult> RestoreShelfAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await unitOfWork.Shelves.FirstOrDefaultAsync(x => x.Id == id, asNoTracking: false, ct);
        if (entity == null)
        {
            return ServiceResult.Failure(ErrorCode.ShelfNotFound);
        }

        entity.Status = EntityStatus.Active;
        unitOfWork.Shelves.Update(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã khôi phục vị trí kệ \"{entity.Code}\".");
    }

    public async Task<ServiceResult> HardDeleteShelfAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        var entity = await unitOfWork.Shelves.FirstOrDefaultAsync(x => x.Id == id, asNoTracking: false, ct);
        if (entity == null)
        {
            return ServiceResult.Failure(ErrorCode.ShelfNotFound);
        }

        if (entity.Status != EntityStatus.Deleted)
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
        }

        if (!IsValidHardDeleteConfirm(confirmCode))
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid);
        }

        var hasAnyCopy = await unitOfWork.BookCopies.ExistsAsync(x => x.ShelfId == id, ct);
        if (hasAnyCopy)
        {
            return ServiceResult.Failure(ErrorCode.CommonRecordInUse);
        }

        unitOfWork.Shelves.Delete(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã xóa vĩnh viễn vị trí kệ \"{entity.Code}\" khỏi cơ sở dữ liệu.");
    }
}
