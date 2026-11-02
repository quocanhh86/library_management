using web_mvc_management.Models.ViewModels.Catalog;
using web_mvc_management.Models.ViewModels.Home;

namespace web_mvc_management.Services;

public interface IBookService
{
    Task<IReadOnlyList<BookSummaryViewModel>> SuggestAsync(string keyword, int limit = 6, CancellationToken ct = default);
    Task<IReadOnlyList<BookSummaryViewModel>> GetNewArrivalsAsync(int count, CancellationToken ct = default);
    Task<IReadOnlyList<TopicViewModel>> GetTopicsAsync(CancellationToken ct = default);
}
