using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.AdminCatalog;
using web_mvc_management.Repositories;

namespace web_mvc_management.Services;

public class BookAdminService(
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    ICloudinaryService cloudinaryService) : IBookAdminService
{
    private sealed record UploadedImagesBatch(
        ImageUploadResult? Cover,
        List<ImageUploadResult> Gallery);

    public async Task<PagedResult<AdminBookListItemViewModel>> GetAdminBooksPagedAsync(
        AdminBookFilter filter,
        CancellationToken ct = default)
    {
        var query = unitOfWork.Books.GetQueryable(asNoTracking: true);

        if (filter.Status == EntityStatus.Deleted)
        {
            query = query.Where(b => b.Status == EntityStatus.Deleted);
        }
        else
        {
            query = query.Where(b => b.Status != EntityStatus.Deleted);
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                query = query.Where(b => b.Status == filter.Status);
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.ToLower();
            query = query.Where(b =>
                b.Title.ToLower().Contains(kw) ||
                (b.Isbn != null && b.Isbn.ToLower().Contains(kw)) ||
                b.BookAuthors.Any(ba => ba.Status != EntityStatus.Deleted && ba.Author.FullName.ToLower().Contains(kw)));
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(b => b.CategoryId == filter.CategoryId.Value);
        }

        if (filter.AuthorId.HasValue)
        {
            query = query.Where(b => b.BookAuthors.Any(ba => ba.AuthorId == filter.AuthorId.Value && ba.Status != EntityStatus.Deleted));
        }

        if (filter.PublisherId.HasValue)
        {
            query = query.Where(b => b.PublisherId == filter.PublisherId.Value);
        }

        if (filter.PublicationYear.HasValue)
        {
            query = query.Where(b => b.PublicationYear == filter.PublicationYear.Value);
        }

        var totalCount = await query.CountAsync(ct);
        var pageNo = filter.NormalizedPageNo;
        var pageSize = filter.PageItemSize;

        var rawItems = await query
            .Include(b => b.Category)
            .Include(b => b.Publisher)
            .Include(b => b.BookAuthors.Where(ba => ba.Status != EntityStatus.Deleted))
                .ThenInclude(ba => ba.Author)
            .Include(b => b.BookImages.Where(img => img.Status != EntityStatus.Deleted))
            .Include(b => b.BookCopies.Where(bc => bc.Status != EntityStatus.Deleted))
                .ThenInclude(bc => bc.Shelf)
            .AsSplitQuery()
            .OrderByDescending(b => b.CreatedAt)
            .Skip(filter.SkipCount)
            .Take(filter.TakeCount)
            .ToListAsync(ct);

        var items = rawItems.Select(MapToAdminListItem).ToList();
        return new PagedResult<AdminBookListItemViewModel>(items, totalCount, pageNo, pageSize);
    }

    public async Task<BookUpsertViewModel?> GetBookForEditAsync(Guid id, CancellationToken ct = default)
    {
        var book = await unitOfWork.Books.GetQueryable(asNoTracking: true)
            .Include(b => b.BookAuthors.Where(ba => ba.Status != EntityStatus.Deleted))
            .Include(b => b.BookImages.Where(img => img.Status != EntityStatus.Deleted))
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == id && b.Status != EntityStatus.Deleted, ct);

        if (book == null)
        {
            return null;
        }

        var existingImages = book.BookImages
            .Where(img => img.Status != EntityStatus.Deleted)
            .OrderByDescending(img => img.IsPrimary)
            .ThenBy(img => img.DisplayOrder)
            .ThenBy(img => img.CreatedAt)
            .Select(img => new BookImageItemViewModel
            {
                Id = img.Id,
                ImageUrl = img.ImageUrl,
                PublicId = img.PublicId,
                ImageType = img.ImageType,
                ImageTypeLabel = LibraryConstants.BookImageType.GetLabel(img.ImageType),
                Caption = img.Caption,
                DisplayOrder = img.DisplayOrder,
                IsPrimary = img.IsPrimary
            })
            .ToList();

        return new BookUpsertViewModel
        {
            Id = book.Id,
            Title = book.Title,
            Isbn = book.Isbn,
            CategoryId = book.CategoryId,
            PublisherId = book.PublisherId,
            AuthorIds = [.. book.BookAuthors
                .Where(ba => ba.Status != EntityStatus.Deleted)
                .OrderBy(ba => ba.AuthorOrder)
                .Select(ba => ba.AuthorId)],
            PublicationYear = book.PublicationYear,
            Edition = book.Edition,
            Language = book.Language,
            PageCount = book.PageCount,
            Description = book.Description,
            CoverUrl = BookCatalogMapper.ResolveValidCoverUrl(book.CoverUrl) ?? existingImages.FirstOrDefault()?.ImageUrl,
            Status = book.Status,
            InitialCopyCount = 0,
            PrimaryImageId = existingImages.FirstOrDefault(x => x.IsPrimary)?.Id,
            ExistingImages = existingImages
        };
    }

    public async Task<ServiceResult<Guid>> CreateBookWithCopiesAsync(BookUpsertViewModel model, CancellationToken ct = default)
    {
        var isbnTrim = NormalizeIsbn(model.Isbn);
        var preCheckIsbn = await CheckDuplicateIsbnAsync(isbnTrim, excludeBookId: null, ct);
        if (preCheckIsbn != null)
        {
            return ServiceResult<Guid>.ValidationError(ErrorCode.BookIsbnAlreadyExists, preCheckIsbn);
        }

        if (model.InitialCopyCount > 0 && (!model.ShelfId.HasValue || model.ShelfId.Value == Guid.Empty))
        {
            return ServiceResult<Guid>.ValidationError(ErrorCode.ShelfNotFound, "Vui lòng chọn vị trí kệ khi nhập số lượng bản sao ban đầu.");
        }

        var uploadResult = await UploadBookImagesAsync(model, ct);
        if (!uploadResult.Succeeded || uploadResult.Data == null)
        {
            return ServiceResult<Guid>.ValidationError(ErrorCode.CommonInvalidRequest, uploadResult.Message);
        }

        var uploaded = uploadResult.Data;
        return await unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            if (isbnTrim != null && await unitOfWork.Books.ExistsAsync(b => b.Isbn == isbnTrim, ct))
            {
                return ServiceResult<Guid>.ValidationError(ErrorCode.BookIsbnAlreadyExists);
            }

            var distinctAuthors = model.AuthorIds.Where(id => id != Guid.Empty).Distinct().ToList();
            var refCheck = await ValidateReferencesAsync(
                model.CategoryId,
                model.PublisherId,
                distinctAuthors,
                model.InitialCopyCount > 0 ? model.ShelfId : null,
                ct);
            if (!refCheck.IsSuccess)
            {
                return ServiceResult<Guid>.Failure(refCheck.Code ?? ErrorCode.CommonValidationError, refCheck.Message);
            }

            var normalizedStatus = LibraryConstants.NormalizeActiveOrInactive(model.Status);
            var directCoverUrl = string.IsNullOrWhiteSpace(model.CoverUrl) ? null : model.CoverUrl.Trim();
            var primaryCoverUrl = uploaded.Cover?.Url ?? directCoverUrl ?? uploaded.Gallery.FirstOrDefault()?.Url;

            var book = new Book
            {
                Id = Guid.NewGuid(),
                Title = model.Title.Trim(),
                Isbn = isbnTrim,
                CategoryId = model.CategoryId,
                PublisherId = model.PublisherId,
                PublicationYear = model.PublicationYear,
                Edition = model.Edition?.Trim(),
                Language = model.Language?.Trim(),
                PageCount = model.PageCount,
                Description = model.Description?.Trim(),
                CoverUrl = primaryCoverUrl,
                Status = normalizedStatus
            };
            await unitOfWork.Books.AddAsync(book, ct);

            await AddInitialAuthorsAsync(book.Id, distinctAuthors, ct);
            await AddInitialImagesAsync(book.Id, directCoverUrl, uploaded, model, ct);
            var generatedCount = await CreateInitialCopiesAsync(book.Id, model, ct);

            var totalImagesUploaded = (uploaded.Cover != null ? 1 : 0) + uploaded.Gallery.Count;
            var imgNote = totalImagesUploaded > 0 ? $" và {totalImagesUploaded} hình ảnh" : string.Empty;
            var msg = generatedCount > 0
                ? $"Đã nhập đầu sách \"{book.Title}\" kèm {generatedCount} bản sao{imgNote} sẵn sàng trên kệ."
                : $"Đã thêm đầu sách \"{book.Title}\"{imgNote}.";

            return ServiceResult<Guid>.Success(book.Id, msg);
        }, cancellationToken: ct);
    }

    public async Task<ServiceResult> UpdateBookAsync(BookUpsertViewModel model, CancellationToken ct = default)
    {
        if (!model.Id.HasValue || model.Id.Value == Guid.Empty)
        {
            return ServiceResult.Failure(ErrorCode.BookNotFound);
        }

        var bookExists = await unitOfWork.Books.ExistsAsync(b => b.Id == model.Id.Value && b.Status != EntityStatus.Deleted, ct);
        if (!bookExists)
        {
            return ServiceResult.Failure(ErrorCode.BookNotFound);
        }

        var isbnTrim = NormalizeIsbn(model.Isbn);
        var preCheckIsbn = await CheckDuplicateIsbnAsync(isbnTrim, excludeBookId: model.Id.Value, ct);
        if (preCheckIsbn != null)
        {
            return ServiceResult.ValidationError(ErrorCode.BookIsbnAlreadyExists, preCheckIsbn);
        }

        var uploadResult = await UploadBookImagesAsync(model, ct);
        if (!uploadResult.Succeeded || uploadResult.Data == null)
        {
            return ServiceResult.ValidationError(ErrorCode.CommonInvalidRequest, uploadResult.Message);
        }

        var uploaded = uploadResult.Data;
        var publicIdsToCleanup = new List<string>();

        var txResult = await unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var book = await unitOfWork.Books.FirstOrDefaultAsync(
                b => b.Id == model.Id.Value && b.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (book == null)
            {
                return ServiceResult.Failure(ErrorCode.BookNotFound);
            }

            if (isbnTrim != null && await unitOfWork.Books.ExistsAsync(b => b.Isbn == isbnTrim && b.Id != book.Id, ct))
            {
                return ServiceResult.ValidationError(ErrorCode.BookIsbnAlreadyExists);
            }

            var distinctAuthors = model.AuthorIds.Where(id => id != Guid.Empty).Distinct().ToList();
            var refCheck = await ValidateReferencesAsync(model.CategoryId, model.PublisherId, distinctAuthors, shelfId: null, ct);
            if (!refCheck.IsSuccess)
            {
                return refCheck;
            }

            book.Title = model.Title.Trim();
            book.Isbn = isbnTrim;
            book.CategoryId = model.CategoryId;
            book.PublisherId = model.PublisherId;
            book.PublicationYear = model.PublicationYear;
            book.Edition = model.Edition?.Trim();
            book.Language = model.Language?.Trim();
            book.PageCount = model.PageCount;
            book.Description = model.Description?.Trim();
            book.CoverUrl = string.IsNullOrWhiteSpace(model.CoverUrl) ? book.CoverUrl : model.CoverUrl.Trim();
            book.Status = LibraryConstants.NormalizeActiveOrInactive(model.Status);

            await SyncUpdatedImagesAsync(book, model, uploaded, publicIdsToCleanup, ct);
            await SyncBookAuthorsAsync(book.Id, distinctAuthors, ct);

            return ServiceResult.Success($"Đã cập nhật thông tin đầu sách \"{book.Title}\".");
        }, cancellationToken: ct);

        if (txResult.IsSuccess)
        {
            foreach (var pid in publicIdsToCleanup)
            {
                await cloudinaryService.DeleteImageAsync(pid, ct);
            }
        }

        return txResult;
    }

    public Task<ServiceResult> DeleteBookAsync(Guid id, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var book = await unitOfWork.Books.FirstOrDefaultAsync(
                b => b.Id == id && b.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (book == null)
            {
                return ServiceResult.Failure(ErrorCode.BookNotFound);
            }

            var hasActiveCopies = await unitOfWork.BookCopies.ExistsAsync(
                c => c.BookId == id &&
                     c.Status != EntityStatus.Deleted &&
                     (c.CopyStatus == LibraryConstants.CopyStatus.Borrowed || c.CopyStatus == LibraryConstants.CopyStatus.Reserved),
                ct) || await unitOfWork.BorrowDetails.ExistsAsync(
                bd => bd.BookCopy.BookId == id && bd.ReturnedAt == null && bd.Status != EntityStatus.Deleted,
                ct);

            if (hasActiveCopies)
            {
                return ServiceResult.Failure(ErrorCode.BookHasBorrowedCopies);
            }

            var hasActiveReservations = await unitOfWork.Reservations.ExistsAsync(
                r => r.BookId == id &&
                     r.Status != EntityStatus.Deleted &&
                     (r.ReservationStatus == LibraryConstants.ReservationStatus.Pending ||
                      r.ReservationStatus == LibraryConstants.ReservationStatus.Ready),
                ct);

            if (hasActiveReservations)
            {
                return ServiceResult.Failure(
                    ErrorCode.CommonRecordInUse,
                    "Không thể xóa mềm đầu sách đang có bạn đọc đặt trước (PENDING/READY). Vui lòng hủy phiếu đặt trước trước.");
            }

            // Cascade soft-delete các bản sao chưa xóa của đầu sách, giữ nguyên CopyStatus vật lý theo docs/01.
            var copies = await unitOfWork.BookCopies.FindAsync(
                c => c.BookId == id && c.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            foreach (var copy in copies)
            {
                copy.Status = EntityStatus.Deleted;
            }

            book.Status = EntityStatus.Deleted;
            return ServiceResult.Success($"Đã xóa mềm đầu sách \"{book.Title}\" (chuyển sang DELETED).");
        }, cancellationToken: ct);

    public Task<ServiceResult> RestoreBookAsync(Guid id, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var book = await unitOfWork.Books.FirstOrDefaultAsync(
                b => b.Id == id && b.Status == EntityStatus.Deleted,
                asNoTracking: false,
                ct);
            if (book == null)
            {
                return ServiceResult.Failure(ErrorCode.BookNotFound);
            }

            if (!string.IsNullOrWhiteSpace(book.Isbn) &&
                await unitOfWork.Books.ExistsAsync(b => b.Id != book.Id && b.Isbn == book.Isbn, ct))
            {
                return ServiceResult.ValidationError(ErrorCode.BookIsbnAlreadyExists);
            }

            book.Status = EntityStatus.Active;
            return ServiceResult.Success($"Đã khôi phục đầu sách \"{book.Title}\" về trạng thái hoạt động.");
        }, cancellationToken: ct);

    public async Task<ServiceResult> HardDeleteBookAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        if (!LibraryConstants.IsValidHardDeleteConfirm(confirmCode))
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid);
        }

        var publicIdsToDelete = new List<string>();
        var txResult = await unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var book = await unitOfWork.Books.FirstOrDefaultAsync(b => b.Id == id, asNoTracking: false, ct);
            if (book == null)
            {
                return ServiceResult.Failure(ErrorCode.BookNotFound);
            }

            if (book.Status != EntityStatus.Deleted)
            {
                return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
            }

            var hasCopiesOrReservations = await unitOfWork.BookCopies.ExistsAsync(c => c.BookId == id, ct) ||
                                          await unitOfWork.Reservations.ExistsAsync(r => r.BookId == id, ct);
            if (hasCopiesOrReservations)
            {
                return ServiceResult.Failure(
                    ErrorCode.CommonRecordInUse,
                    "Không thể xóa vĩnh viễn đầu sách vì vẫn còn bản sao vật lý hoặc lịch sử đặt trước liên kết. Vui lòng xóa vĩnh viễn các bản sao trước.");
            }

            var authorLinks = await unitOfWork.BookAuthors.FindAsync(ba => ba.BookId == id, asNoTracking: false, ct);
            if (authorLinks.Count > 0)
            {
                unitOfWork.BookAuthors.DeleteRange(authorLinks);
            }

            var bookImages = await unitOfWork.BookImages.FindAsync(img => img.BookId == id, asNoTracking: false, ct);
            if (bookImages.Count > 0)
            {
                publicIdsToDelete.AddRange(
                    bookImages.Where(x => !string.IsNullOrWhiteSpace(x.PublicId)).Select(x => x.PublicId!));
                unitOfWork.BookImages.DeleteRange(bookImages);
            }

            var title = book.Title;
            unitOfWork.Books.Delete(book);
            return ServiceResult.Success($"Đã xóa vĩnh viễn đầu sách \"{title}\" khỏi cơ sở dữ liệu.");
        }, cancellationToken: ct);

        if (txResult.IsSuccess)
        {
            foreach (var publicId in publicIdsToDelete)
            {
                await cloudinaryService.DeleteImageAsync(publicId, ct);
            }
        }

        return txResult;
    }

    // ===== Helpers nội bộ =====

    private static string? NormalizeIsbn(string? rawIsbn) =>
        string.IsNullOrWhiteSpace(rawIsbn) ? null : rawIsbn.Trim();

    private async Task<string?> CheckDuplicateIsbnAsync(string? isbnTrim, Guid? excludeBookId, CancellationToken ct)
    {
        if (isbnTrim == null)
        {
            return null;
        }

        var duplicate = await unitOfWork.Books.FirstOrDefaultAsync(
            b => b.Isbn == isbnTrim && (excludeBookId == null || b.Id != excludeBookId.Value),
            asNoTracking: true,
            ct);

        if (duplicate == null)
        {
            return null;
        }

        return duplicate.Status == EntityStatus.Deleted
            ? $"Mã ISBN \"{isbnTrim}\" đang thuộc về đầu sách \"{duplicate.Title}\" trong mục Đã xóa mềm. Vui lòng khôi phục hoặc xóa vĩnh viễn đầu sách cũ trước."
            : ErrorMessages.Get(ErrorCode.BookIsbnAlreadyExists);
    }

    private async Task<ServiceResult<UploadedImagesBatch>> UploadBookImagesAsync(BookUpsertViewModel model, CancellationToken ct)
    {
        ImageUploadResult? coverUpload = null;
        if (model.CoverImageFile is { Length: > 0 })
        {
            var uploadRes = await cloudinaryService.UploadBookImageAsync(model.CoverImageFile, ct);
            if (!uploadRes.Succeeded || uploadRes.Data == null)
            {
                return ServiceResult<UploadedImagesBatch>.ValidationError(ErrorCode.CommonInvalidRequest, uploadRes.Message);
            }
            coverUpload = uploadRes.Data;
        }

        var galleryUploads = new List<ImageUploadResult>();
        if (model.GalleryFiles is { Count: > 0 })
        {
            foreach (var file in model.GalleryFiles.Where(f => f is { Length: > 0 }))
            {
                var uploadRes = await cloudinaryService.UploadBookImageAsync(file, ct);
                if (!uploadRes.Succeeded || uploadRes.Data == null)
                {
                    return ServiceResult<UploadedImagesBatch>.ValidationError(ErrorCode.CommonInvalidRequest, uploadRes.Message);
                }
                galleryUploads.Add(uploadRes.Data);
            }
        }

        return ServiceResult<UploadedImagesBatch>.Success(new UploadedImagesBatch(coverUpload, galleryUploads));
    }

    private async Task<ServiceResult> ValidateReferencesAsync(
        Guid? categoryId,
        Guid? publisherId,
        List<Guid> distinctAuthors,
        Guid? shelfId,
        CancellationToken ct)
    {
        if (categoryId.HasValue && !await unitOfWork.Categories.ExistsAsync(c => c.Id == categoryId.Value && c.Status != EntityStatus.Deleted, ct))
        {
            return ServiceResult.Failure(ErrorCode.CategoryNotFound);
        }

        if (publisherId.HasValue && !await unitOfWork.Publishers.ExistsAsync(p => p.Id == publisherId.Value && p.Status != EntityStatus.Deleted, ct))
        {
            return ServiceResult.Failure(ErrorCode.PublisherNotFound);
        }

        if (shelfId.HasValue && !await unitOfWork.Shelves.ExistsAsync(s => s.Id == shelfId.Value && s.Status != EntityStatus.Deleted, ct))
        {
            return ServiceResult.Failure(ErrorCode.ShelfNotFound);
        }

        if (distinctAuthors.Count == 0)
        {
            return ServiceResult.ValidationError(ErrorCode.AuthorNotFound, "Vui lòng chọn ít nhất một tác giả hợp lệ.");
        }

        var validAuthorCount = await unitOfWork.Authors.CountAsync(
            a => distinctAuthors.Contains(a.Id) && a.Status != EntityStatus.Deleted, ct);
        if (validAuthorCount != distinctAuthors.Count)
        {
            return ServiceResult.Failure(ErrorCode.AuthorNotFound);
        }

        return ServiceResult.Success();
    }

    private async Task AddInitialAuthorsAsync(Guid bookId, List<Guid> distinctAuthors, CancellationToken ct)
    {
        short order = 1;
        foreach (var authorId in distinctAuthors)
        {
            await unitOfWork.BookAuthors.AddAsync(new BookAuthor
            {
                BookId = bookId,
                AuthorId = authorId,
                AuthorOrder = order++,
                Status = EntityStatus.Active
            }, ct);
        }
    }

    private async Task AddInitialImagesAsync(
        Guid bookId,
        string? directCoverUrl,
        UploadedImagesBatch uploaded,
        BookUpsertViewModel model,
        CancellationToken ct)
    {
        var displayOrder = 0;
        var hasPrimaryAssigned = false;

        if (uploaded.Cover != null || !string.IsNullOrWhiteSpace(directCoverUrl))
        {
            await unitOfWork.BookImages.AddAsync(new BookImage
            {
                Id = Guid.NewGuid(),
                BookId = bookId,
                ImageUrl = uploaded.Cover?.Url ?? directCoverUrl!,
                PublicId = uploaded.Cover?.PublicId,
                ImageType = LibraryConstants.BookImageType.Cover,
                Caption = "Ảnh bìa chính",
                DisplayOrder = displayOrder++,
                IsPrimary = true,
                Status = EntityStatus.Active
            }, ct);
            hasPrimaryAssigned = true;
        }

        var galleryType = string.IsNullOrWhiteSpace(model.GalleryImageType)
            ? LibraryConstants.BookImageType.PreviewPage
            : model.GalleryImageType;

        for (var idx = 0; idx < uploaded.Gallery.Count; idx++)
        {
            var item = uploaded.Gallery[idx];
            var caption = !string.IsNullOrWhiteSpace(model.GalleryCaptionPrefix)
                ? (uploaded.Gallery.Count > 1 ? $"{model.GalleryCaptionPrefix.Trim()} - Trang {idx + 1}" : model.GalleryCaptionPrefix.Trim())
                : $"{LibraryConstants.BookImageType.GetLabel(galleryType)} #{idx + 1}";

            await unitOfWork.BookImages.AddAsync(new BookImage
            {
                Id = Guid.NewGuid(),
                BookId = bookId,
                ImageUrl = item.Url,
                PublicId = item.PublicId,
                ImageType = galleryType,
                Caption = caption,
                DisplayOrder = displayOrder++,
                IsPrimary = !hasPrimaryAssigned && idx == 0,
                Status = EntityStatus.Active
            }, ct);
        }
    }

    private async Task<int> CreateInitialCopiesAsync(Guid bookId, BookUpsertViewModel model, CancellationToken ct)
    {
        if (model.InitialCopyCount <= 0)
        {
            return 0;
        }

        var utcNow = dateTimeProvider.UtcNow;
        var acquired = model.AcquiredDate ?? dateTimeProvider.Today;
        var generatedBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < model.InitialCopyCount; i++)
        {
            var barcode = await BookCatalogMapper.GenerateUniqueBarcodeAsync(unitOfWork, utcNow, generatedBarcodes, ct);
            generatedBarcodes.Add(barcode);

            await unitOfWork.BookCopies.AddAsync(new BookCopy
            {
                Id = Guid.NewGuid(),
                BookId = bookId,
                ShelfId = model.ShelfId,
                Barcode = barcode,
                CopyStatus = LibraryConstants.CopyStatus.Available,
                Price = model.CopyPrice,
                AcquiredDate = acquired,
                Note = model.CopyNote?.Trim(),
                Status = EntityStatus.Active
            }, ct);
        }

        return model.InitialCopyCount;
    }

    private async Task SyncUpdatedImagesAsync(
        Book book,
        BookUpsertViewModel model,
        UploadedImagesBatch uploaded,
        List<string> publicIdsToCleanup,
        CancellationToken ct)
    {
        var existingImages = await unitOfWork.BookImages.FindAsync(
            img => img.BookId == book.Id && img.Status != EntityStatus.Deleted,
            asNoTracking: false,
            ct);

        var deleteSet = model.DeleteImageIds?.ToHashSet() ?? [];
        foreach (var img in existingImages.Where(x => deleteSet.Contains(x.Id)))
        {
            img.Status = EntityStatus.Deleted;
            img.IsPrimary = false;
            if (!string.IsNullOrWhiteSpace(img.PublicId))
            {
                publicIdsToCleanup.Add(img.PublicId);
            }
        }

        var activeImages = existingImages.Where(x => x.Status != EntityStatus.Deleted).ToList();

        if (uploaded.Cover != null)
        {
            foreach (var img in activeImages)
            {
                img.IsPrimary = false;
            }

            var newCoverImg = new BookImage
            {
                Id = Guid.NewGuid(),
                BookId = book.Id,
                ImageUrl = uploaded.Cover.Url,
                PublicId = uploaded.Cover.PublicId,
                ImageType = LibraryConstants.BookImageType.Cover,
                Caption = "Ảnh bìa chính",
                DisplayOrder = 0,
                IsPrimary = true,
                Status = EntityStatus.Active
            };
            await unitOfWork.BookImages.AddAsync(newCoverImg, ct);
            activeImages.Insert(0, newCoverImg);
            book.CoverUrl = uploaded.Cover.Url;
        }
        else if (model.PrimaryImageId.HasValue)
        {
            var chosenPrimary = activeImages.FirstOrDefault(x => x.Id == model.PrimaryImageId.Value);
            if (chosenPrimary != null)
            {
                foreach (var img in activeImages)
                {
                    img.IsPrimary = img.Id == chosenPrimary.Id;
                }
                book.CoverUrl = chosenPrimary.ImageUrl;
            }
        }

        var nextOrder = activeImages.Count > 0 ? activeImages.Max(x => x.DisplayOrder) + 1 : 1;
        var galleryType = string.IsNullOrWhiteSpace(model.GalleryImageType)
            ? LibraryConstants.BookImageType.PreviewPage
            : model.GalleryImageType;

        for (var idx = 0; idx < uploaded.Gallery.Count; idx++)
        {
            var item = uploaded.Gallery[idx];
            var caption = !string.IsNullOrWhiteSpace(model.GalleryCaptionPrefix)
                ? (uploaded.Gallery.Count > 1 ? $"{model.GalleryCaptionPrefix.Trim()} - Trang {idx + 1}" : model.GalleryCaptionPrefix.Trim())
                : $"{LibraryConstants.BookImageType.GetLabel(galleryType)} #{nextOrder + idx}";

            var galleryImg = new BookImage
            {
                Id = Guid.NewGuid(),
                BookId = book.Id,
                ImageUrl = item.Url,
                PublicId = item.PublicId,
                ImageType = galleryType,
                Caption = caption,
                DisplayOrder = nextOrder + idx,
                IsPrimary = false,
                Status = EntityStatus.Active
            };
            await unitOfWork.BookImages.AddAsync(galleryImg, ct);
            activeImages.Add(galleryImg);
        }

        if (activeImages.Count > 0 && !activeImages.Any(x => x.IsPrimary))
        {
            var firstImg = activeImages.OrderBy(x => x.DisplayOrder).First();
            firstImg.IsPrimary = true;
            book.CoverUrl = firstImg.ImageUrl;
        }
        else if (activeImages.Count == 0 && deleteSet.Count > 0 && uploaded.Cover == null)
        {
            book.CoverUrl = string.IsNullOrWhiteSpace(model.CoverUrl) ? null : model.CoverUrl.Trim();
        }
    }

    private async Task SyncBookAuthorsAsync(Guid bookId, List<Guid> distinctAuthors, CancellationToken ct)
    {
        var existingLinks = await unitOfWork.BookAuthors.FindAsync(
            ba => ba.BookId == bookId,
            asNoTracking: false,
            ct);

        var existingByAuthorId = existingLinks.ToDictionary(ba => ba.AuthorId);

        foreach (var link in existingLinks.Where(link => !distinctAuthors.Contains(link.AuthorId) && link.Status != EntityStatus.Deleted))
        {
            link.Status = EntityStatus.Deleted;
        }

        short order = 1;
        foreach (var authorId in distinctAuthors)
        {
            if (existingByAuthorId.TryGetValue(authorId, out var existingLink))
            {
                existingLink.AuthorOrder = order++;
                existingLink.Status = EntityStatus.Active;
            }
            else
            {
                await unitOfWork.BookAuthors.AddAsync(new BookAuthor
                {
                    BookId = bookId,
                    AuthorId = authorId,
                    AuthorOrder = order++,
                    Status = EntityStatus.Active
                }, ct);
            }
        }
    }

    private static AdminBookListItemViewModel MapToAdminListItem(Book b)
    {
        var activeCopies = b.BookCopies.Where(c => c.Status != EntityStatus.Deleted).ToList();
        var nonDisposed = activeCopies.Where(c => c.CopyStatus != LibraryConstants.CopyStatus.Disposed).ToList();
        var shelves = nonDisposed.Where(c => c.Shelf != null).Select(c => c.Shelf!.Code).Distinct().ToList();
        var authors = b.BookAuthors
            .Where(ba => ba.Status != EntityStatus.Deleted && ba.Author != null)
            .OrderBy(ba => ba.AuthorOrder)
            .Select(ba => ba.Author.FullName)
            .ToList();
        var activeImages = b.BookImages
            .Where(img => img.Status != EntityStatus.Deleted)
            .OrderByDescending(img => img.IsPrimary)
            .ThenBy(img => img.DisplayOrder)
            .ToList();

        return new AdminBookListItemViewModel
        {
            Id = b.Id,
            Title = b.Title,
            Isbn = b.Isbn,
            CoverUrl = BookCatalogMapper.ResolveValidCoverUrl(b.CoverUrl) ?? activeImages.FirstOrDefault()?.ImageUrl,
            ImageCount = activeImages.Count,
            CategoryId = b.CategoryId,
            CategoryName = b.Category?.Name ?? "Chưa phân loại",
            PublisherId = b.PublisherId,
            PublisherName = b.Publisher?.Name ?? "Chưa rõ NXB",
            AuthorsText = authors.Count > 0 ? string.Join(", ", authors) : "Chưa gán tác giả",
            PublicationYear = b.PublicationYear,
            Edition = b.Edition,
            Language = b.Language,
            Status = b.Status,
            TotalCopies = nonDisposed.Count,
            AvailableCopies = nonDisposed.Count(c => c.CopyStatus == LibraryConstants.CopyStatus.Available),
            BorrowedCopies = nonDisposed.Count(c => c.CopyStatus == LibraryConstants.CopyStatus.Borrowed || c.CopyStatus == LibraryConstants.CopyStatus.Reserved),
            DamagedOrLostCopies = nonDisposed.Count(c => c.CopyStatus == LibraryConstants.CopyStatus.Damaged || c.CopyStatus == LibraryConstants.CopyStatus.Lost),
            DisposedCopies = activeCopies.Count(c => c.CopyStatus == LibraryConstants.CopyStatus.Disposed),
            ShelfSummary = shelves.Count > 0 ? string.Join(", ", shelves) : "—",
            CreatedAt = b.CreatedAt
        };
    }
}
