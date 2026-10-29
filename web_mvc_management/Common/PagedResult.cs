namespace web_mvc_management.Common;

/// <summary>
/// Chứa kết quả phân trang dùng chung cho các màn hình danh sách (Razor View & PartialView).
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int TotalCount { get; init; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;
    public int StartItemIndex => TotalCount == 0 ? 0 : (PageIndex - 1) * PageSize + 1;
    public int EndItemIndex => TotalCount == 0 ? 0 : Math.Min(PageIndex * PageSize, TotalCount);

    public PagedResult() { }

    public PagedResult(IReadOnlyList<T> items, int count, int pageIndex, int pageSize)
    {
        Items = items;
        TotalCount = count;
        PageIndex = pageIndex < 1 ? 1 : pageIndex;
        PageSize = pageSize < 1 ? 10 : pageSize;
    }

    public static PagedResult<T> Empty(int pageIndex = 1, int pageSize = 10) =>
        new([], 0, pageIndex, pageSize);
}

