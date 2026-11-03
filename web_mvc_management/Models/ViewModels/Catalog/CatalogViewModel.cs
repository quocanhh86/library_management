using System;
using System.Collections.Generic;
using web_mvc_management.Models.ViewModels.Home;

namespace web_mvc_management.Models.ViewModels.Catalog;

public record SelectOptionViewModel(Guid Id, string Name);

public class CatalogViewModel
{
    public IReadOnlyList<BookSummaryViewModel> Books { get; init; } = [];
    public IReadOnlyList<TopicViewModel> Topics { get; init; } = [];
    public IReadOnlyList<SelectOptionViewModel> Authors { get; init; } = [];
    public IReadOnlyList<SelectOptionViewModel> Publishers { get; init; } = [];
    public string? Keyword { get; init; }
    public string? Category { get; init; }
    public Guid? AuthorId { get; init; }
    public Guid? PublisherId { get; init; }
    public int? Year { get; init; }
    public bool Available { get; init; }
    public int Page { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
