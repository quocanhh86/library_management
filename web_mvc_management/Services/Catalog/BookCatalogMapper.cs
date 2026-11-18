using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Helper;
using web_mvc_management.Models.ViewModels.Catalog;
using web_mvc_management.Repositories;
using web_mvc_management.Util;

namespace web_mvc_management.Services;

/// <summary>
/// Trình ánh xạ và tiện ích dùng chung cho toàn bộ miền Danh mục sách.
/// </summary>
internal static class BookCatalogMapper
{
    public static string? ResolveValidCoverUrl(string? rawUrl)
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

    public static BookSummaryViewModel MapToSummaryViewModel(Book b, bool includeCopies)
    {
        var activeCopies = b.BookCopies
            .Where(c => c.Status != EntityStatus.Deleted && c.CopyStatus != LibraryConstants.CopyStatus.Disposed)
            .ToList();

        var availableCount = activeCopies.Count(c => c.CopyStatus == LibraryConstants.CopyStatus.Available);
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
            CategoryKey = ResolveCategoryKey(categoryName),
            DocumentScope = documentScope,
            DocumentScopeLabel = GetDocumentScopeLabel(documentScope),
            AvailableCopies = availableCount,
            TotalCopies = activeCopies.Count,
            EarliestDueDate = earliestDue,
            Copies = copyViewModels,
            Images = imageViewModels
        };
    }

    public static async Task<string> GenerateUniqueBarcodeAsync(
        IUnitOfWork unitOfWork,
        DateTimeOffset utcNow,
        HashSet<string> batchSet,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = BarcodeHelper.GenerateBarcode(utcNow);
            if (batchSet.Contains(candidate))
            {
                continue;
            }

            if (!await unitOfWork.BookCopies.ExistsAsync(c => c.Barcode == candidate, ct))
            {
                return candidate;
            }
        }

        return $"BC-{utcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
    }

    public static string? NormalizeDocumentScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        return scope.Trim().ToLowerInvariant() switch
        {
            "textbook" or "giaotrinh" => "textbook",
            "thesis" or "luanvan" or "doan" => "thesis",
            "journal" or "tapchi" => "journal",
            _ => null
        };
    }

    public static string ResolveDocumentScope(Book b)
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

    public static string GetDocumentScopeLabel(string scope) => scope switch
    {
        "thesis" => "Luận văn & Đồ án",
        "journal" => "Tạp chí khoa học",
        _ => "Giáo trình"
    };

    public static string ResolveCategoryKey(string categoryName)
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
