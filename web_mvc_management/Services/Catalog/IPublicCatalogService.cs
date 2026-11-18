using web_mvc_management.Models.ViewModels.Catalog;

namespace web_mvc_management.Services;

/// <summary>
/// Dịch vụ tra cứu danh mục sách công khai cho trang chủ, tìm kiếm và chi tiết tài liệu.
/// </summary>
public interface IPublicCatalogService
{
    Task<IReadOnlyList<BookSummaryViewModel>> SuggestAsync(string keyword, int limit = 6, string? scope = null, CancellationToken ct = default);

    Task<IReadOnlyList<BookSummaryViewModel>> GetNewArrivalsAsync(int count, CancellationToken ct = default);

    Task<CatalogViewModel> GetPublicCatalogAsync(
        string? keyword,
        string? category,
        Guid? authorId,
        Guid? publisherId,
        int? year,
        bool available,
        int page,
        string? scope = null,
        CancellationToken ct = default);

    Task<BookSummaryViewModel?> GetPublicBookDetailAsync(Guid id, CancellationToken ct = default);
}
