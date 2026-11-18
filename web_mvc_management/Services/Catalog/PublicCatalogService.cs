using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Models.ViewModels.Catalog;
using web_mvc_management.Repositories;
using web_mvc_management.Util;

namespace web_mvc_management.Services;

public class PublicCatalogService(
    IUnitOfWork unitOfWork,
    ICatalogLookupService catalogLookup) : IPublicCatalogService
{
    public async Task<IReadOnlyList<BookSummaryViewModel>> GetNewArrivalsAsync(int count, CancellationToken ct = default)
    {
        var books = await ActiveCatalogQuery()
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.PublicationYear)
            .Take(count)
            .ToListAsync(ct);

        return [.. books.Select(b => BookCatalogMapper.MapToSummaryViewModel(b, includeCopies: false))];
    }

    public async Task<IReadOnlyList<BookSummaryViewModel>> SuggestAsync(
        string keyword,
        int limit = 6,
        string? scope = null,
        CancellationToken ct = default)
    {
        var term = StringUtil.RemoveVietnameseSigns(keyword.Trim()).ToLowerInvariant();
        if (term.Length < 2)
        {
            return [];
        }

        var allActive = await GetNewArrivalsAsync(200, ct);
        var normalizedScope = BookCatalogMapper.NormalizeDocumentScope(scope);

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
        var query = ActiveCatalogQuery();

        if (!string.IsNullOrWhiteSpace(category) && Guid.TryParse(category, out var categoryGuid))
        {
            query = query.Where(b => b.CategoryId == categoryGuid);
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

        var books = await query.OrderByDescending(b => b.CreatedAt).ToListAsync(ct);
        var mapped = books.Select(b => BookCatalogMapper.MapToSummaryViewModel(b, includeCopies: false)).ToList();

        var normalizedScope = BookCatalogMapper.NormalizeDocumentScope(scope);
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

        return new CatalogViewModel
        {
            Books = [.. mapped.Skip(PaginationParams.CalculateSkip(safePage, pageSize)).Take(pageSize)],
            Topics = await catalogLookup.GetTopicsAsync(ct),
            Authors = await catalogLookup.GetActiveAuthorsAsync(ct),
            Publishers = await catalogLookup.GetActivePublishersAsync(ct),
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

        return book == null ? null : BookCatalogMapper.MapToSummaryViewModel(book, includeCopies: true);
    }

    private IQueryable<Book> ActiveCatalogQuery() =>
        unitOfWork.Books.GetQueryable(asNoTracking: true)
            .Include(b => b.Category)
            .Include(b => b.Publisher)
            .Include(b => b.BookAuthors.Where(ba => ba.Status != EntityStatus.Deleted))
                .ThenInclude(ba => ba.Author)
            .Include(b => b.BookCopies.Where(bc => bc.Status != EntityStatus.Deleted && bc.CopyStatus != LibraryConstants.CopyStatus.Disposed))
                .ThenInclude(bc => bc.Shelf)
            .Include(b => b.BookCopies.Where(bc => bc.Status != EntityStatus.Deleted && bc.CopyStatus != LibraryConstants.CopyStatus.Disposed))
                .ThenInclude(bc => bc.BorrowDetails.Where(bd => bd.ReturnedAt == null && bd.Status != EntityStatus.Deleted))
            .Where(b => b.Status == EntityStatus.Active);
}
