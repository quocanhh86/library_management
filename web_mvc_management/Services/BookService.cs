using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Extensions;
using web_mvc_management.Helper;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.AdminCatalog;
using web_mvc_management.Models.ViewModels.Catalog;
using web_mvc_management.Models.ViewModels.Home;
using web_mvc_management.Repositories;
using web_mvc_management.Util;

namespace web_mvc_management.Services;

public class BookService(
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    ICloudinaryService cloudinaryService) : IBookService
{
    // =========================================================================
    // 1. TRA CỨU DANH MỤC SÁCH CÔNG KHAI (PUBLIC & READER PORTAL)
    // =========================================================================
    public async Task<IReadOnlyList<BookSummaryViewModel>> GetNewArrivalsAsync(int count, CancellationToken ct = default)
    {
        var books = await unitOfWork.Books.GetQueryable(asNoTracking: true)
            .Include(b => b.Category)
            .Include(b => b.Publisher)
            .Include(b => b.BookAuthors.Where(ba => ba.Status != EntityStatus.Deleted))
                .ThenInclude(ba => ba.Author)
            .Include(b => b.BookImages.Where(img => img.Status != EntityStatus.Deleted))
            .Include(b => b.BookCopies.Where(bc => bc.Status != EntityStatus.Deleted && bc.CopyStatus != LibraryConstants.CopyStatus.Disposed))
                .ThenInclude(bc => bc.Shelf)
            .Include(b => b.BookCopies.Where(bc => bc.Status != EntityStatus.Deleted && bc.CopyStatus != LibraryConstants.CopyStatus.Disposed))
                .ThenInclude(bc => bc.BorrowDetails.Where(bd => bd.ReturnedAt == null && bd.Status != EntityStatus.Deleted))
            .Where(b => b.Status == EntityStatus.Active)
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.PublicationYear)
            .Take(count)
            .ToListAsync(ct);

        return [.. books.Select(b => MapToSummaryViewModel(b, includeCopies: false))];
    }

    public async Task<IReadOnlyList<TopicViewModel>> GetTopicsAsync(CancellationToken ct = default)
    {
        var categories = await unitOfWork.Categories.GetQueryable(asNoTracking: true)
            .Where(c => c.Status == EntityStatus.Active)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        return [.. categories.Select(c => new TopicViewModel(c.Id.ToString(), c.Name))];
    }

    public async Task<IReadOnlyList<SelectOptionViewModel>> GetActiveAuthorsSelectAsync(CancellationToken ct = default)
    {
        return await unitOfWork.Authors.GetQueryable(asNoTracking: true)
            .Where(a => a.Status == EntityStatus.Active)
            .OrderBy(a => a.FullName)
            .Select(a => new SelectOptionViewModel(a.Id, a.FullName))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SelectOptionViewModel>> GetActivePublishersSelectAsync(CancellationToken ct = default)
    {
        return await unitOfWork.Publishers.GetQueryable(asNoTracking: true)
            .Where(p => p.Status == EntityStatus.Active)
            .OrderBy(p => p.Name)
            .Select(p => new SelectOptionViewModel(p.Id, p.Name))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BookSummaryViewModel>> SuggestAsync(string keyword, int limit = 6, string? scope = null, CancellationToken ct = default)
    {
        var term = StringUtil.RemoveVietnameseSigns(keyword.Trim()).ToLowerInvariant();
        if (term.Length < 2)
        {
            return [];
        }

        var allActive = await GetNewArrivalsAsync(200, ct);
        var normalizedScope = NormalizeDocumentScope(scope);

        return [.. allActive
            .Where(b => string.IsNullOrEmpty(normalizedScope) || b.DocumentScope.Equals(normalizedScope, StringComparison.OrdinalIgnoreCase))
            .Where(b => StringUtil.RemoveVietnameseSigns($"{b.Title} {b.Authors} {b.Isbn}").ToLowerInvariant().Contains(term))
            .Take(limit)];
    }

    public async Task<CatalogViewModel> GetPublicCatalogAsync(
        string? keyword,
        string? category,
        Guid? authorId,
        Guid? publisherId,
        int? year,
        bool available,
        int page,
        string? scope = null,
        CancellationToken ct = default)
    {
        var query = unitOfWork.Books.GetQueryable(asNoTracking: true)
            .Include(b => b.Category)
            .Include(b => b.Publisher)
            .Include(b => b.BookAuthors.Where(ba => ba.Status != EntityStatus.Deleted))
                .ThenInclude(ba => ba.Author)
            .Include(b => b.BookImages.Where(img => img.Status != EntityStatus.Deleted))
            .Include(b => b.BookCopies.Where(bc => bc.Status != EntityStatus.Deleted && bc.CopyStatus != LibraryConstants.CopyStatus.Disposed))
                .ThenInclude(bc => bc.Shelf)
            .Include(b => b.BookCopies.Where(bc => bc.Status != EntityStatus.Deleted && bc.CopyStatus != LibraryConstants.CopyStatus.Disposed))
                .ThenInclude(bc => bc.BorrowDetails.Where(bd => bd.ReturnedAt == null && bd.Status != EntityStatus.Deleted))
            .Where(b => b.Status == EntityStatus.Active);

        if (!string.IsNullOrWhiteSpace(category))
        {
            if (Guid.TryParse(category, out var categoryGuid))
            {
                query = query.Where(b => b.CategoryId == categoryGuid);
            }
        }

        if (authorId.HasValue)
        {
            query = query.Where(b => b.BookAuthors.Any(ba => ba.AuthorId == authorId.Value && ba.Status != EntityStatus.Deleted));
        }

        if (publisherId.HasValue)
        {
            query = query.Where(b => b.PublisherId == publisherId.Value);
        }

        if (year.HasValue)
        {
            query = query.Where(b => b.PublicationYear == (short)year.Value);
        }

        var books = await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

        var mapped = books.Select(b => MapToSummaryViewModel(b, includeCopies: false)).ToList();

        var normalizedScope = NormalizeDocumentScope(scope);
        if (!string.IsNullOrEmpty(normalizedScope))
        {
            mapped = [.. mapped.Where(b => b.DocumentScope.Equals(normalizedScope, StringComparison.OrdinalIgnoreCase))];
        }

        if (!string.IsNullOrWhiteSpace(category) && !Guid.TryParse(category, out _))
        {
            mapped = [.. mapped.Where(b => b.CategoryKey.Equals(category, StringComparison.OrdinalIgnoreCase))];
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = StringUtil.RemoveVietnameseSigns(keyword.Trim()).ToLowerInvariant();
            mapped = [.. mapped.Where(b =>
                StringUtil.RemoveVietnameseSigns($"{b.Title} {b.Authors} {b.Publisher} {b.Isbn}").ToLowerInvariant().Contains(term))];
        }

        if (available)
        {
            mapped = [.. mapped.Where(b => b.AvailableCopies > 0)];
        }

        const int pageSize = 10;
        var totalCount = mapped.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var safePage = Math.Clamp(page <= 0 ? 1 : page, 1, totalPages);

        var topics = await GetTopicsAsync(ct);
        var authors = await GetActiveAuthorsSelectAsync(ct);
        var publishers = await GetActivePublishersSelectAsync(ct);

        return new CatalogViewModel
        {
            Books = [.. mapped.Skip(PaginationParams.CalculateSkip(safePage, pageSize)).Take(pageSize)],
            Topics = topics,
            Authors = authors,
            Publishers = publishers,
            Keyword = keyword,
            Scope = normalizedScope,
            Category = category,
            AuthorId = authorId,
            PublisherId = publisherId,
            Year = year,
            Available = available,
            Page = safePage,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task<BookSummaryViewModel?> GetPublicBookDetailAsync(Guid id, CancellationToken ct = default)
    {
        var book = await unitOfWork.Books.GetQueryable(asNoTracking: true)
            .Include(b => b.Category)
            .Include(b => b.Publisher)
            .Include(b => b.BookAuthors.Where(ba => ba.Status != EntityStatus.Deleted))
                .ThenInclude(ba => ba.Author)
            .Include(b => b.BookImages.Where(img => img.Status != EntityStatus.Deleted))
            .Include(b => b.BookCopies.Where(bc => bc.Status != EntityStatus.Deleted))
                .ThenInclude(bc => bc.Shelf)
            .Include(b => b.BookCopies.Where(bc => bc.Status != EntityStatus.Deleted))
                .ThenInclude(bc => bc.BorrowDetails.Where(bd => bd.ReturnedAt == null && bd.Status != EntityStatus.Deleted))
            .FirstOrDefaultAsync(b => b.Id == id && b.Status != EntityStatus.Deleted, ct);

        if (book == null)
        {
            return null;
        }

        return MapToSummaryViewModel(book, includeCopies: true);
    }

    // =========================================================================
    // 2. QUẢN TRỊ ĐẦU SÁCH & NHẬP SÁCH MỚI (/Admin/Books)
    // =========================================================================
    public async Task<PagedResult<AdminBookListItemViewModel>> GetAdminBooksPagedAsync(AdminBookFilter filter, CancellationToken ct = default)
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
            .OrderByDescending(b => b.CreatedAt)
            .Skip(filter.SkipCount)
            .Take(filter.TakeCount)
            .ToListAsync(ct);

        var items = rawItems.Select(b =>
        {
            var activeCopies = b.BookCopies.Where(c => c.Status != EntityStatus.Deleted).ToList();
            var nonDisposed = activeCopies.Where(c => c.CopyStatus != LibraryConstants.CopyStatus.Disposed).ToList();
            var shelves = nonDisposed
                .Where(c => c.Shelf != null)
                .Select(c => c.Shelf!.Code)
                .Distinct()
                .ToList();

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

            var resolvedCover = ResolveValidCoverUrl(b.CoverUrl)
                ?? activeImages.FirstOrDefault()?.ImageUrl;

            return new AdminBookListItemViewModel
            {
                Id = b.Id,
                Title = b.Title,
                Isbn = b.Isbn,
                CoverUrl = resolvedCover,
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
        }).ToList();

        return new PagedResult<AdminBookListItemViewModel>(items, totalCount, pageNo, pageSize);
    }

    public async Task<BookUpsertViewModel?> GetBookForEditAsync(Guid id, CancellationToken ct = default)
    {
        var book = await unitOfWork.Books.GetQueryable(asNoTracking: true)
            .Include(b => b.BookAuthors.Where(ba => ba.Status != EntityStatus.Deleted))
            .Include(b => b.BookImages.Where(img => img.Status != EntityStatus.Deleted))
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
            CoverUrl = ResolveValidCoverUrl(book.CoverUrl) ?? existingImages.FirstOrDefault()?.ImageUrl,
            Status = book.Status,
            InitialCopyCount = 0,
            PrimaryImageId = existingImages.FirstOrDefault(x => x.IsPrimary)?.Id,
            ExistingImages = existingImages
        };
    }

    public async Task<ServiceResult<Guid>> CreateBookWithCopiesAsync(BookUpsertViewModel model, CancellationToken ct = default)
    {
        var isbnTrim = string.IsNullOrWhiteSpace(model.Isbn) ? null : model.Isbn.Trim();
        if (isbnTrim != null)
        {
            var existingIsbnBook = await unitOfWork.Books.FirstOrDefaultAsync(
                b => b.Isbn == isbnTrim,
                asNoTracking: true,
                ct);
            if (existingIsbnBook != null)
            {
                var isbnMsg = existingIsbnBook.Status == EntityStatus.Deleted
                    ? $"Mã ISBN \"{isbnTrim}\" đang thuộc về đầu sách \"{existingIsbnBook.Title}\" trong mục Đã xóa mềm. Vui lòng khôi phục hoặc xóa vĩnh viễn đầu sách cũ trước."
                    : null;
                return ServiceResult<Guid>.ValidationError(ErrorCode.BookIsbnAlreadyExists, isbnMsg);
            }
        }

        if (model.InitialCopyCount > 0 && (!model.ShelfId.HasValue || model.ShelfId.Value == Guid.Empty))
        {
            return ServiceResult<Guid>.ValidationError(ErrorCode.ShelfNotFound, "Vui lòng chọn vị trí kệ khi nhập số lượng bản sao ban đầu.");
        }

        // 1. Xử lý upload file ảnh bìa & các ảnh trang sách lên Cloudinary / Local
        ImageUploadResult? coverUpload = null;
        if (model.CoverImageFile is { Length: > 0 })
        {
            var uploadRes = await cloudinaryService.UploadBookImageAsync(model.CoverImageFile, ct);
            if (!uploadRes.Succeeded || uploadRes.Data == null)
            {
                return ServiceResult<Guid>.ValidationError(ErrorCode.CommonInvalidRequest, uploadRes.Message);
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
                    return ServiceResult<Guid>.ValidationError(ErrorCode.CommonInvalidRequest, uploadRes.Message);
                }

                galleryUploads.Add(uploadRes.Data);
            }
        }

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            if (isbnTrim != null)
            {
                var duplicateInTx = await unitOfWork.Books.ExistsAsync(b => b.Isbn == isbnTrim, ct);
                if (duplicateInTx)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult<Guid>.ValidationError(ErrorCode.BookIsbnAlreadyExists);
                }
            }

            if (model.CategoryId.HasValue)
            {
                var categoryOk = await unitOfWork.Categories.ExistsAsync(
                    c => c.Id == model.CategoryId.Value && c.Status != EntityStatus.Deleted, ct);
                if (!categoryOk)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult<Guid>.Failure(ErrorCode.CategoryNotFound);
                }
            }

            if (model.PublisherId.HasValue)
            {
                var publisherOk = await unitOfWork.Publishers.ExistsAsync(
                    p => p.Id == model.PublisherId.Value && p.Status != EntityStatus.Deleted, ct);
                if (!publisherOk)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult<Guid>.Failure(ErrorCode.PublisherNotFound);
                }
            }

            if (model.InitialCopyCount > 0 && model.ShelfId.HasValue)
            {
                var shelfOk = await unitOfWork.Shelves.ExistsAsync(
                    s => s.Id == model.ShelfId.Value && s.Status != EntityStatus.Deleted, ct);
                if (!shelfOk)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult<Guid>.Failure(ErrorCode.ShelfNotFound);
                }
            }

            var directCoverUrl = string.IsNullOrWhiteSpace(model.CoverUrl) ? null : model.CoverUrl.Trim();
            var primaryCoverUrl = coverUpload?.Url
                ?? directCoverUrl
                ?? galleryUploads.FirstOrDefault()?.Url;

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
                Status = model.Status
            };

            await unitOfWork.Books.AddAsync(book, ct);

            // Liên kết danh sách tác giả
            var distinctAuthors = model.AuthorIds.Distinct().ToList();
            short order = 1;
            foreach (var authorId in distinctAuthors)
            {
                await unitOfWork.BookAuthors.AddAsync(new BookAuthor
                {
                    BookId = book.Id,
                    AuthorId = authorId,
                    AuthorOrder = order++,
                    Status = EntityStatus.Active
                }, ct);
            }

            // Lưu danh sách hình ảnh đầu sách (BookImages)
            var displayOrder = 0;
            var hasPrimaryAssigned = false;

            if (coverUpload != null)
            {
                await unitOfWork.BookImages.AddAsync(new BookImage
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    ImageUrl = coverUpload.Url,
                    PublicId = coverUpload.PublicId,
                    ImageType = LibraryConstants.BookImageType.Cover,
                    Caption = "Ảnh bìa chính",
                    DisplayOrder = displayOrder++,
                    IsPrimary = true,
                    Status = EntityStatus.Active
                }, ct);
                hasPrimaryAssigned = true;
            }
            else if (!string.IsNullOrWhiteSpace(directCoverUrl))
            {
                await unitOfWork.BookImages.AddAsync(new BookImage
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    ImageUrl = directCoverUrl,
                    PublicId = null,
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

            for (var idx = 0; idx < galleryUploads.Count; idx++)
            {
                var item = galleryUploads[idx];
                var isFirstPrimary = !hasPrimaryAssigned && idx == 0;
                var caption = !string.IsNullOrWhiteSpace(model.GalleryCaptionPrefix)
                    ? (galleryUploads.Count > 1 ? $"{model.GalleryCaptionPrefix.Trim()} - Trang {idx + 1}" : model.GalleryCaptionPrefix.Trim())
                    : $"{LibraryConstants.BookImageType.GetLabel(galleryType)} #{idx + 1}";

                await unitOfWork.BookImages.AddAsync(new BookImage
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    ImageUrl = item.Url,
                    PublicId = item.PublicId,
                    ImageType = galleryType,
                    Caption = caption,
                    DisplayOrder = displayOrder++,
                    IsPrimary = isFirstPrimary,
                    Status = EntityStatus.Active
                }, ct);
            }

            // Tự động sinh N bản sao vật lý kèm mã vạch nếu có yêu cầu nhập kho
            var generatedCount = 0;
            if (model.InitialCopyCount > 0)
            {
                var acquired = model.AcquiredDate ?? dateTimeProvider.Today;
                var generatedBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < model.InitialCopyCount; i++)
                {
                    var barcode = await GenerateUniqueBarcodeAsync(generatedBarcodes, ct);
                    generatedBarcodes.Add(barcode);

                    await unitOfWork.BookCopies.AddAsync(new BookCopy
                    {
                        Id = Guid.NewGuid(),
                        BookId = book.Id,
                        ShelfId = model.ShelfId,
                        Barcode = barcode,
                        CopyStatus = LibraryConstants.CopyStatus.Available,
                        Price = model.CopyPrice,
                        AcquiredDate = acquired,
                        Note = model.CopyNote?.Trim(),
                        Status = EntityStatus.Active
                    }, ct);
                    generatedCount++;
                }
            }

            await unitOfWork.CommitTransactionAsync(ct);

            var totalImagesUploaded = (coverUpload != null ? 1 : 0) + galleryUploads.Count;
            var imgNote = totalImagesUploaded > 0 ? $" và {totalImagesUploaded} hình ảnh" : string.Empty;
            var msg = generatedCount > 0
                ? $"Đã nhập đầu sách \"{book.Title}\" kèm {generatedCount} bản sao{imgNote} sẵn sàng trên kệ."
                : $"Đã thêm đầu sách \"{book.Title}\"{imgNote}.";

            return ServiceResult<Guid>.Success(book.Id, msg);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> UpdateBookAsync(BookUpsertViewModel model, CancellationToken ct = default)
    {
        if (!model.Id.HasValue || model.Id.Value == Guid.Empty)
        {
            return ServiceResult.Failure(ErrorCode.BookNotFound);
        }

        var book = await unitOfWork.Books.FirstOrDefaultAsync(
            b => b.Id == model.Id.Value && b.Status != EntityStatus.Deleted,
            asNoTracking: false,
            ct);

        if (book == null)
        {
            return ServiceResult.Failure(ErrorCode.BookNotFound);
        }

        var isbnTrim = string.IsNullOrWhiteSpace(model.Isbn) ? null : model.Isbn.Trim();
        if (isbnTrim != null)
        {
            var duplicateIsbnBook = await unitOfWork.Books.FirstOrDefaultAsync(
                b => b.Isbn == isbnTrim && b.Id != book.Id,
                asNoTracking: true,
                ct);
            if (duplicateIsbnBook != null)
            {
                var isbnMsg = duplicateIsbnBook.Status == EntityStatus.Deleted
                    ? $"Mã ISBN \"{isbnTrim}\" đang thuộc về đầu sách \"{duplicateIsbnBook.Title}\" trong mục Đã xóa mềm. Vui lòng khôi phục hoặc xóa vĩnh viễn đầu sách cũ trước."
                    : null;
                return ServiceResult.ValidationError(ErrorCode.BookIsbnAlreadyExists, isbnMsg);
            }
        }

        // Upload file ảnh mới nếu có
        ImageUploadResult? coverUpload = null;
        if (model.CoverImageFile is { Length: > 0 })
        {
            var uploadRes = await cloudinaryService.UploadBookImageAsync(model.CoverImageFile, ct);
            if (!uploadRes.Succeeded || uploadRes.Data == null)
            {
                return ServiceResult.ValidationError(ErrorCode.CommonInvalidRequest, uploadRes.Message);
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
                    return ServiceResult.ValidationError(ErrorCode.CommonInvalidRequest, uploadRes.Message);
                }

                galleryUploads.Add(uploadRes.Data);
            }
        }

        var publicIdsToCleanup = new List<string>();

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            if (isbnTrim != null)
            {
                var duplicateInTx = await unitOfWork.Books.ExistsAsync(
                    b => b.Isbn == isbnTrim && b.Id != book.Id, ct);
                if (duplicateInTx)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult.ValidationError(ErrorCode.BookIsbnAlreadyExists);
                }
            }

            if (model.CategoryId.HasValue)
            {
                var categoryOk = await unitOfWork.Categories.ExistsAsync(
                    c => c.Id == model.CategoryId.Value && c.Status != EntityStatus.Deleted, ct);
                if (!categoryOk)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult.Failure(ErrorCode.CategoryNotFound);
                }
            }

            if (model.PublisherId.HasValue)
            {
                var publisherOk = await unitOfWork.Publishers.ExistsAsync(
                    p => p.Id == model.PublisherId.Value && p.Status != EntityStatus.Deleted, ct);
                if (!publisherOk)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult.Failure(ErrorCode.PublisherNotFound);
                }
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
            book.Status = model.Status;

            // Xử lý danh sách hình ảnh hiện có (BookImages)
            var existingImages = await unitOfWork.BookImages.FindAsync(
                img => img.BookId == book.Id && img.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);

            var deleteSet = model.DeleteImageIds?.ToHashSet() ?? [];
            foreach (var img in existingImages.Where(x => deleteSet.Contains(x.Id)))
            {
                img.Status = EntityStatus.Deleted;
                img.IsPrimary = false;
                unitOfWork.BookImages.Update(img);
                if (!string.IsNullOrWhiteSpace(img.PublicId))
                {
                    publicIdsToCleanup.Add(img.PublicId);
                }
            }

            var activeImages = existingImages.Where(x => x.Status != EntityStatus.Deleted).ToList();

            // Nếu người dùng tải lên ảnh bìa chính mới
            if (coverUpload != null)
            {
                foreach (var img in activeImages)
                {
                    if (img.IsPrimary)
                    {
                        img.IsPrimary = false;
                        unitOfWork.BookImages.Update(img);
                    }
                }

                var newCoverImg = new BookImage
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    ImageUrl = coverUpload.Url,
                    PublicId = coverUpload.PublicId,
                    ImageType = LibraryConstants.BookImageType.Cover,
                    Caption = "Ảnh bìa chính",
                    DisplayOrder = 0,
                    IsPrimary = true,
                    Status = EntityStatus.Active
                };

                await unitOfWork.BookImages.AddAsync(newCoverImg, ct);
                activeImages.Insert(0, newCoverImg);
                book.CoverUrl = coverUpload.Url;
            }
            else if (model.PrimaryImageId.HasValue)
            {
                var chosenPrimary = activeImages.FirstOrDefault(x => x.Id == model.PrimaryImageId.Value);
                if (chosenPrimary != null)
                {
                    foreach (var img in activeImages)
                    {
                        var shouldBePrimary = img.Id == chosenPrimary.Id;
                        if (img.IsPrimary != shouldBePrimary)
                        {
                            img.IsPrimary = shouldBePrimary;
                            unitOfWork.BookImages.Update(img);
                        }
                    }

                    book.CoverUrl = chosenPrimary.ImageUrl;
                }
            }

            // Thêm các ảnh trang sách / mục lục / bìa sau mới tải lên
            var nextOrder = activeImages.Count > 0 ? activeImages.Max(x => x.DisplayOrder) + 1 : 1;
            var galleryType = string.IsNullOrWhiteSpace(model.GalleryImageType)
                ? LibraryConstants.BookImageType.PreviewPage
                : model.GalleryImageType;

            for (var idx = 0; idx < galleryUploads.Count; idx++)
            {
                var item = galleryUploads[idx];
                var caption = !string.IsNullOrWhiteSpace(model.GalleryCaptionPrefix)
                    ? (galleryUploads.Count > 1 ? $"{model.GalleryCaptionPrefix.Trim()} - Trang {idx + 1}" : model.GalleryCaptionPrefix.Trim())
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

            // Đảm bảo luôn có 1 ảnh IsPrimary nếu danh sách activeImages không rỗng
            if (activeImages.Count > 0 && !activeImages.Any(x => x.IsPrimary))
            {
                var firstImg = activeImages.OrderBy(x => x.DisplayOrder).First();
                firstImg.IsPrimary = true;
                book.CoverUrl = firstImg.ImageUrl;
            }
            else if (activeImages.Count == 0 && deleteSet.Count > 0 && coverUpload == null)
            {
                book.CoverUrl = string.IsNullOrWhiteSpace(model.CoverUrl) ? null : model.CoverUrl.Trim();
            }

            unitOfWork.Books.Update(book);

            // Đồng bộ lại danh sách tác giả (BookAuthors) bằng Xóa mềm (Soft Delete)
            var existingLinks = await unitOfWork.BookAuthors.FindAsync(
                ba => ba.BookId == book.Id,
                asNoTracking: false,
                ct);

            var distinctAuthors = model.AuthorIds.Distinct().ToList();
            var existingByAuthorId = existingLinks.ToDictionary(ba => ba.AuthorId);

            foreach (var link in existingLinks)
            {
                if (!distinctAuthors.Contains(link.AuthorId) && link.Status != EntityStatus.Deleted)
                {
                    link.Status = EntityStatus.Deleted;
                    unitOfWork.BookAuthors.Update(link);
                }
            }

            short order = 1;
            foreach (var authorId in distinctAuthors)
            {
                if (existingByAuthorId.TryGetValue(authorId, out var existingLink))
                {
                    existingLink.AuthorOrder = order++;
                    existingLink.Status = EntityStatus.Active;
                    unitOfWork.BookAuthors.Update(existingLink);
                }
                else
                {
                    await unitOfWork.BookAuthors.AddAsync(new BookAuthor
                    {
                        BookId = book.Id,
                        AuthorId = authorId,
                        AuthorOrder = order++,
                        Status = EntityStatus.Active
                    }, ct);
                }
            }

            await unitOfWork.CommitTransactionAsync(ct);

            foreach (var pid in publicIdsToCleanup)
            {
                await cloudinaryService.DeleteImageAsync(pid, ct);
            }

            return ServiceResult.Success($"Đã cập nhật thông tin đầu sách \"{book.Title}\".");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> DeleteBookAsync(Guid id, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var book = await unitOfWork.Books.FirstOrDefaultAsync(
                b => b.Id == id && b.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);

            if (book == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
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
                await unitOfWork.RollbackTransactionAsync(ct);
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
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(
                    ErrorCode.CommonRecordInUse,
                    "Không thể xóa mềm đầu sách đang có bạn đọc đặt trước (PENDING/READY). Vui lòng hủy phiếu đặt trước trước.");
            }

            book.Status = EntityStatus.Deleted;
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã xóa mềm đầu sách \"{book.Title}\" (chuyển sang DELETED).");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> RestoreBookAsync(Guid id, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var book = await unitOfWork.Books.FirstOrDefaultAsync(
                b => b.Id == id && b.Status == EntityStatus.Deleted,
                asNoTracking: false,
                ct);

            if (book == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.BookNotFound);
            }

            if (!string.IsNullOrWhiteSpace(book.Isbn))
            {
                var duplicateIsbn = await unitOfWork.Books.ExistsAsync(
                    b => b.Id != book.Id && b.Isbn == book.Isbn,
                    ct);
                if (duplicateIsbn)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult.ValidationError(ErrorCode.BookIsbnAlreadyExists);
                }
            }

            book.Status = EntityStatus.Active;
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã khôi phục đầu sách \"{book.Title}\" về trạng thái hoạt động.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> HardDeleteBookAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        if (!string.Equals(confirmCode?.Trim(), "XOA", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid);
        }

        var publicIdsToDelete = new List<string>();

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var book = await unitOfWork.Books.FirstOrDefaultAsync(b => b.Id == id, asNoTracking: false, ct);
            if (book == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.BookNotFound);
            }

            if (book.Status != EntityStatus.Deleted)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
            }

            var hasCopiesOrReservations = await unitOfWork.BookCopies.ExistsAsync(c => c.BookId == id, ct) ||
                                          await unitOfWork.Reservations.ExistsAsync(r => r.BookId == id, ct);

            if (hasCopiesOrReservations)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
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
            await unitOfWork.CommitTransactionAsync(ct);

            foreach (var publicId in publicIdsToDelete)
            {
                await cloudinaryService.DeleteImageAsync(publicId, ct);
            }

            return ServiceResult.Success($"Đã xóa vĩnh viễn đầu sách \"{title}\" khỏi cơ sở dữ liệu.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // =========================================================================
    // 3. QUẢN LÝ BẢN SAO / MÃ VẠCH & THANH LÝ, HỦY SÁCH HỎNG/MẤT (/Admin/BookCopies)
    // =========================================================================
    public async Task<PagedResult<AdminBookCopyListItemViewModel>> GetBookCopiesPagedAsync(AdminBookCopyFilter filter, CancellationToken ct = default)
    {
        var query = unitOfWork.BookCopies.GetQueryable(asNoTracking: true);

        if (filter.Status == EntityStatus.Deleted)
        {
            query = query.Where(c => c.Status == EntityStatus.Deleted);
        }
        else
        {
            query = query.Where(c => c.Status != EntityStatus.Deleted && c.Book.Status != EntityStatus.Deleted);
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                query = query.Where(c => c.Status == filter.Status);
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.ToLower();
            query = query.Where(c =>
                c.Barcode.ToLower().Contains(kw) ||
                c.Book.Title.ToLower().Contains(kw) ||
                (c.Book.Isbn != null && c.Book.Isbn.ToLower().Contains(kw)));
        }

        if (filter.BookId.HasValue)
        {
            query = query.Where(c => c.BookId == filter.BookId.Value);
        }

        if (filter.ShelfId.HasValue)
        {
            query = query.Where(c => c.ShelfId == filter.ShelfId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.CopyStatus))
        {
            query = query.Where(c => c.CopyStatus == filter.CopyStatus);
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Barcode)
            .Select(c => new AdminBookCopyListItemViewModel
            {
                Id = c.Id,
                BookId = c.BookId,
                BookTitle = c.Book.Title,
                Isbn = c.Book.Isbn,
                Barcode = c.Barcode,
                ShelfId = c.ShelfId,
                ShelfCode = c.Shelf != null ? c.Shelf.Code : "Chưa xếp kệ",
                CopyStatus = c.CopyStatus,
                Status = c.Status,
                Price = c.Price,
                AcquiredDate = c.AcquiredDate,
                DisposedAt = c.DisposedAt,
                DisposalReason = c.DisposalReason,
                Note = c.Note
            })
            .ToPagedResultAsync(filter, ct);
    }

    public async Task<ServiceResult<int>> CreateBookCopiesBatchAsync(BookCopyBatchCreateViewModel model, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var book = await unitOfWork.Books.FirstOrDefaultAsync(
                b => b.Id == model.BookId && b.Status != EntityStatus.Deleted,
                asNoTracking: true,
                ct);

            if (book == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult<int>.Failure(ErrorCode.BookNotFound);
            }

            var shelfExists = await unitOfWork.Shelves.ExistsAsync(
                s => s.Id == model.ShelfId && s.Status != EntityStatus.Deleted,
                ct);

            if (!shelfExists)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult<int>.Failure(ErrorCode.ShelfNotFound);
            }

            if (model.Quantity == 1 && !string.IsNullOrWhiteSpace(model.CustomBarcode))
            {
                var customTrim = model.CustomBarcode.Trim().ToUpperInvariant();
                if (await unitOfWork.BookCopies.ExistsAsync(c => c.Barcode.ToUpper() == customTrim, ct))
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult<int>.ValidationError(ErrorCode.BookCopyBarcodeAlreadyExists);
                }

                await unitOfWork.BookCopies.AddAsync(new BookCopy
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    ShelfId = model.ShelfId,
                    Barcode = customTrim,
                    CopyStatus = LibraryConstants.CopyStatus.Available,
                    Price = model.Price,
                    AcquiredDate = model.AcquiredDate ?? dateTimeProvider.Today,
                    Note = model.Note?.Trim(),
                    Status = EntityStatus.Active
                }, ct);

                await unitOfWork.CommitTransactionAsync(ct);
                return ServiceResult<int>.Success(1, $"Đã nhập bản sao mã vạch {customTrim} cho sách \"{book.Title}\".");
            }

            var generatedBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var acquiredDate = model.AcquiredDate ?? dateTimeProvider.Today;

            for (var i = 0; i < model.Quantity; i++)
            {
                var barcode = await GenerateUniqueBarcodeAsync(generatedBarcodes, ct);
                generatedBarcodes.Add(barcode);

                await unitOfWork.BookCopies.AddAsync(new BookCopy
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    ShelfId = model.ShelfId,
                    Barcode = barcode,
                    CopyStatus = LibraryConstants.CopyStatus.Available,
                    Price = model.Price,
                    AcquiredDate = acquiredDate,
                    Note = model.Note?.Trim(),
                    Status = EntityStatus.Active
                }, ct);
            }

            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult<int>.Success(model.Quantity, $"Đã nhập bổ sung {model.Quantity} bản sao mới cho đầu sách \"{book.Title}\".");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> UpdateBookCopyAsync(BookCopyUpdateViewModel model, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var copy = await unitOfWork.BookCopies.FirstOrDefaultAsync(
                c => c.Id == model.Id && c.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);

            if (copy == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.BookCopyNotFound);
            }

            if (model.ShelfId.HasValue)
            {
                var shelfExists = await unitOfWork.Shelves.ExistsAsync(
                    s => s.Id == model.ShelfId.Value && s.Status != EntityStatus.Deleted,
                    ct);
                if (!shelfExists)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult.Failure(ErrorCode.ShelfNotFound);
                }
            }

            copy.ShelfId = model.ShelfId;
            copy.Price = model.Price;
            copy.Note = model.Note?.Trim();

            if (model.RestoreToAvailable)
            {
                if (copy.CopyStatus == LibraryConstants.CopyStatus.Borrowed)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult.Failure(ErrorCode.BookCopyCurrentlyBorrowed);
                }

                if (copy.CopyStatus == LibraryConstants.CopyStatus.Reserved)
                {
                    await unitOfWork.RollbackTransactionAsync(ct);
                    return ServiceResult.Failure(
                        ErrorCode.CommonRecordInUse,
                        $"Bản sao mã vạch {copy.Barcode} đang được giữ chỗ (RESERVED) cho bạn đọc đặt trước.");
                }

                copy.CopyStatus = LibraryConstants.CopyStatus.Available;
                copy.DisposedAt = null;
                copy.DisposalReason = null;
            }

            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã cập nhật bản sao mã vạch {copy.Barcode}.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> DisposeOrMarkCopyAsync(BookCopyDisposeViewModel model, CancellationToken ct = default)
    {
        var validStatuses = new[]
        {
            LibraryConstants.CopyStatus.Damaged,
            LibraryConstants.CopyStatus.Lost,
            LibraryConstants.CopyStatus.Disposed
        };

        if (!validStatuses.Contains(model.TargetStatus))
        {
            return ServiceResult.Failure(ErrorCode.CommonInvalidRequest);
        }

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var copy = await unitOfWork.BookCopies.FirstOrDefaultAsync(
                c => c.Id == model.CopyId && c.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);

            if (copy == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.BookCopyNotFound);
            }

            if (copy.CopyStatus == LibraryConstants.CopyStatus.Borrowed ||
                await unitOfWork.BorrowDetails.ExistsAsync(
                    bd => bd.CopyId == copy.Id && bd.ReturnedAt == null && bd.Status != EntityStatus.Deleted, ct))
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.BookCopyCurrentlyBorrowed);
            }

            if (copy.CopyStatus == LibraryConstants.CopyStatus.Reserved ||
                await unitOfWork.Reservations.ExistsAsync(
                    r => r.AssignedCopyId == copy.Id &&
                         r.Status != EntityStatus.Deleted &&
                         r.ReservationStatus == LibraryConstants.ReservationStatus.Ready, ct))
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(
                    ErrorCode.CommonRecordInUse,
                    $"Bản sao mã vạch {copy.Barcode} đang được giữ chỗ (RESERVED) cho bạn đọc đặt trước. Vui lòng hủy phiếu đặt trước trước khi thanh lý.");
            }

            copy.CopyStatus = model.TargetStatus;
            copy.DisposedAt = dateTimeProvider.UtcNow;
            copy.DisposalReason = model.DisposalReason.Trim();
            if (!string.IsNullOrWhiteSpace(model.Note))
            {
                copy.Note = model.Note.Trim();
            }

            await unitOfWork.CommitTransactionAsync(ct);

            var actionName = model.TargetStatus switch
            {
                LibraryConstants.CopyStatus.Damaged => "ghi nhận hư hỏng",
                LibraryConstants.CopyStatus.Lost => "ghi nhận mất",
                _ => "thanh lý"
            };

            return ServiceResult.Success($"Đã {actionName} bản sao mã vạch {copy.Barcode}.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> SoftDeleteBookCopyAsync(Guid id, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var copy = await unitOfWork.BookCopies.FirstOrDefaultAsync(
                c => c.Id == id && c.Status != EntityStatus.Deleted,
                asNoTracking: false,
                ct);

            if (copy == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.BookCopyNotFound);
            }

            if (copy.CopyStatus == LibraryConstants.CopyStatus.Borrowed ||
                await unitOfWork.BorrowDetails.ExistsAsync(
                    bd => bd.CopyId == copy.Id && bd.ReturnedAt == null && bd.Status != EntityStatus.Deleted, ct))
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.BookCopyCurrentlyBorrowed);
            }

            if (copy.CopyStatus == LibraryConstants.CopyStatus.Reserved ||
                await unitOfWork.Reservations.ExistsAsync(
                    r => r.AssignedCopyId == copy.Id &&
                         r.Status != EntityStatus.Deleted &&
                         r.ReservationStatus == LibraryConstants.ReservationStatus.Ready, ct))
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(
                    ErrorCode.CommonRecordInUse,
                    $"Bản sao mã vạch {copy.Barcode} đang được giữ chỗ (RESERVED) cho bạn đọc đặt trước. Vui lòng hủy phiếu đặt trước trước khi xóa mềm.");
            }

            copy.Status = EntityStatus.Deleted;
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã xóa mềm bản sao mã vạch {copy.Barcode} (chuyển sang DELETED).");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> RestoreBookCopyAsync(Guid id, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var copy = await unitOfWork.BookCopies.FirstOrDefaultAsync(
                c => c.Id == id && c.Status == EntityStatus.Deleted,
                asNoTracking: false,
                ct);

            if (copy == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.BookCopyNotFound);
            }

            copy.Status = EntityStatus.Active;
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã khôi phục bản sao mã vạch {copy.Barcode} về trạng thái hoạt động.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> HardDeleteBookCopyAsync(Guid id, string? confirmCode, CancellationToken ct = default)
    {
        if (!string.Equals(confirmCode?.Trim(), "XOA", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult.Failure(ErrorCode.HardDeleteConfirmInvalid);
        }

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            var copy = await unitOfWork.BookCopies.FirstOrDefaultAsync(c => c.Id == id, asNoTracking: false, ct);
            if (copy == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.BookCopyNotFound);
            }

            if (copy.Status != EntityStatus.Deleted)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(ErrorCode.HardDeleteRequiresSoftDeleteFirst);
            }

            var hasBorrowHistory = await unitOfWork.BorrowDetails.ExistsAsync(bd => bd.CopyId == id, ct) ||
                                   await unitOfWork.Reservations.ExistsAsync(r => r.AssignedCopyId == id, ct);
            if (hasBorrowHistory)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult.Failure(
                    ErrorCode.CommonRecordInUse,
                    "Không thể xóa vĩnh viễn bản sao đã phát sinh lịch sử mượn trả hoặc giữ chỗ đặt trước.");
            }

            var barcode = copy.Barcode;
            unitOfWork.BookCopies.Delete(copy);
            await unitOfWork.CommitTransactionAsync(ct);
            return ServiceResult.Success($"Đã xóa vĩnh viễn bản sao mã vạch {barcode} khỏi cơ sở dữ liệu.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // =========================================================================
    // HELPER METHODS
    // =========================================================================
    private async Task<string> GenerateUniqueBarcodeAsync(HashSet<string> batchSet, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = BarcodeHelper.GenerateBarcode();
            if (batchSet.Contains(candidate))
            {
                continue;
            }

            var existsInDb = await unitOfWork.BookCopies.ExistsAsync(c => c.Barcode == candidate, ct);
            if (!existsInDb)
            {
                return candidate;
            }
        }

        return $"BC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
    }

    private static string? ResolveValidCoverUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return null;
        }

        // Bỏ qua các đường dẫn ảnh tĩnh mẫu cũ /images/books/*.webp không tồn tại trên đĩa
        if (rawUrl.StartsWith("/images/books/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return rawUrl;
    }

    private static BookSummaryViewModel MapToSummaryViewModel(Book b, bool includeCopies)
    {
        var activeCopies = b.BookCopies
            .Where(c => c.Status != EntityStatus.Deleted && c.CopyStatus != LibraryConstants.CopyStatus.Disposed)
            .ToList();

        var availableCount = activeCopies.Count(c => c.CopyStatus == LibraryConstants.CopyStatus.Available);
        var totalCount = activeCopies.Count;

        var earliestDue = activeCopies
            .SelectMany(c => c.BorrowDetails)
            .Where(bd => bd.ReturnedAt == null && bd.Status != EntityStatus.Deleted)
            .OrderBy(bd => bd.DueDate)
            .Select(bd => (DateOnly?)bd.DueDate)
            .FirstOrDefault();

        var authorsList = b.BookAuthors
            .Where(ba => ba.Status != EntityStatus.Deleted && ba.Author != null)
            .OrderBy(ba => ba.AuthorOrder)
            .ToList();

        var authorsText = authorsList.Count > 0
            ? string.Join(", ", authorsList.Select(ba => ba.Author.FullName))
            : "Nhiều tác giả";

        var primaryShelf = activeCopies
            .Where(c => c.Shelf != null)
            .Select(c => c.Shelf!.Code)
            .FirstOrDefault() ?? "KHO-CHUNG";

        var categoryName = b.Category?.Name ?? "Chuyên ngành chung";
        var categoryKey = ResolveCategoryKey(categoryName);

        var imageViewModels = b.BookImages
            .Where(img => img.Status != EntityStatus.Deleted)
            .OrderByDescending(img => img.IsPrimary)
            .ThenBy(img => img.DisplayOrder)
            .ThenBy(img => img.CreatedAt)
            .Select(img => new PublicBookImageViewModel
            {
                Id = img.Id,
                ImageUrl = img.ImageUrl,
                ImageType = img.ImageType,
                ImageTypeLabel = LibraryConstants.BookImageType.GetLabel(img.ImageType),
                Caption = img.Caption ?? LibraryConstants.BookImageType.GetLabel(img.ImageType),
                DisplayOrder = img.DisplayOrder,
                IsPrimary = img.IsPrimary
            })
            .ToList();

        var coverUrl = ResolveValidCoverUrl(b.CoverUrl)
            ?? imageViewModels.FirstOrDefault(img => img.IsPrimary)?.ImageUrl
            ?? imageViewModels.FirstOrDefault()?.ImageUrl;

        if (!string.IsNullOrWhiteSpace(coverUrl) && imageViewModels.Count == 0)
        {
            imageViewModels.Add(new PublicBookImageViewModel
            {
                Id = Guid.Empty,
                ImageUrl = coverUrl,
                ImageType = LibraryConstants.BookImageType.Cover,
                ImageTypeLabel = LibraryConstants.BookImageType.GetLabel(LibraryConstants.BookImageType.Cover),
                Caption = "Ảnh bìa chính",
                DisplayOrder = 0,
                IsPrimary = true
            });
        }

        var copyViewModels = includeCopies
            ? b.BookCopies
                .Where(c => c.Status != EntityStatus.Deleted)
                .OrderBy(c => c.Barcode)
                .Select(c => new PublicBookCopyViewModel
                {
                    Id = c.Id,
                    Barcode = c.Barcode,
                    ShelfCode = c.Shelf?.Code ?? "Chưa xếp kệ",
                    ShelfDescription = c.Shelf?.Description ?? string.Empty,
                    CopyStatus = c.CopyStatus
                })
                .ToList()
            : [];

        var documentScope = ResolveDocumentScope(b);

        return new BookSummaryViewModel
        {
            Id = b.Id,
            Title = b.Title,
            Authors = authorsText,
            AuthorIds = [.. authorsList.Select(ba => ba.AuthorId)],
            Publisher = b.Publisher?.Name ?? "NXB Đại học",
            PublisherId = b.PublisherId,
            PublicationYear = b.PublicationYear,
            CoverUrl = coverUrl,
            Isbn = b.Isbn,
            Edition = b.Edition,
            Language = b.Language,
            PageCount = b.PageCount,
            Description = b.Description,
            CallNumber = primaryShelf,
            CategoryId = b.CategoryId,
            CategoryName = categoryName,
            CategoryKey = categoryKey,
            DocumentScope = documentScope,
            DocumentScopeLabel = GetDocumentScopeLabel(documentScope),
            AvailableCopies = availableCount,
            TotalCopies = totalCount,
            EarliestDueDate = earliestDue,
            Copies = copyViewModels,
            Images = imageViewModels
        };
    }

    private static string? NormalizeDocumentScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var s = scope.Trim().ToLowerInvariant();
        return s switch
        {
            "textbook" or "giaotrinh" => "textbook",
            "thesis" or "luanvan" or "doan" => "thesis",
            "journal" or "tapchi" => "journal",
            _ => null
        };
    }

    private static string ResolveDocumentScope(Book b)
    {
        var combined = StringUtil.RemoveVietnameseSigns($"{b.Title} {b.Edition} {b.Description}").ToLowerInvariant();
        if (combined.Contains("luan van") || combined.Contains("do an") || combined.Contains("khoa luan") ||
            combined.Contains("luan an") || combined.Contains("tot nghiep") || combined.Contains("thac si"))
        {
            return "thesis";
        }

        if (combined.Contains("tap chi") || combined.Contains("journal") || combined.Contains("ky yeu") ||
            combined.Contains("hoi nghi khoa hoc") || combined.Contains("issn"))
        {
            return "journal";
        }

        return "textbook";
    }

    private static string GetDocumentScopeLabel(string scope) => scope switch
    {
        "thesis" => "Luận văn & Đồ án",
        "journal" => "Tạp chí khoa học",
        _ => "Giáo trình"
    };

    private static string ResolveCategoryKey(string categoryName)
    {
        var norm = StringUtil.RemoveVietnameseSigns(categoryName).ToLowerInvariant();
        if (norm.Contains("cong nghe") || norm.Contains("lap trinh") || norm.Contains("co so du lieu") || norm.Contains("he thong"))
        {
            return "it";
        }

        if (norm.Contains("giao thong") || norm.Contains("cau duong") || norm.Contains("xay dung"))
        {
            return "civil";
        }

        if (norm.Contains("kinh te") || norm.Contains("logistics") || norm.Contains("quan tri"))
        {
            return "logistics";
        }

        if (norm.Contains("toan") || norm.Contains("khoa hoc"))
        {
            return "science";
        }

        if (norm.Contains("trac dia"))
        {
            return "survey";
        }

        return "humanities";
    }
}
