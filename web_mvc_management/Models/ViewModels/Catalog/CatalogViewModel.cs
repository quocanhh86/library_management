using web_mvc_management.Models.ViewModels.Home;

namespace web_mvc_management.Models.ViewModels.Catalog;

public class CatalogViewModel
{
    public IReadOnlyList<BookSummaryViewModel> Books { get; init; } = [];
    public IReadOnlyList<TopicViewModel> Topics { get; init; } = [];
    public string? Keyword { get; init; }
    public string? Category { get; init; }
    public int? Year { get; init; }
    public bool Available { get; init; }
    public int Page { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
