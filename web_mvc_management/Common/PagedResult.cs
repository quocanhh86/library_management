namespace web_mvc_management.Common;

/// <summary>
/// Chứa kết quả phân trang dùng chung cho các màn hình danh sách (Razor View và PartialView).
/// Triển khai IPagedResult để tương thích hoàn toàn với _Pagination.cshtml.
/// </summary>
public class PagedResult<T> : IPagedResult
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int PageIndex { get; init; } = 1;
    public int PageNo => PageIndex;

    public int PageSize { get; init; } = PaginationParams.DefaultPageItemSize;
    public int PageItemSize => PageSize;

    public int TotalCount { get; init; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;
    public int StartItemIndex => Items.Count == 0 ? 0 : (int)Math.Min(TotalCount, (long)PaginationParams.CalculateSkip(PageIndex, PageSize) + 1);
    public int EndItemIndex => Items.Count == 0 ? 0 : (int)Math.Min((long)PageIndex * PageSize, TotalCount);

    public PagedResult() { }

    public PagedResult(IReadOnlyList<T> items, int count, int pageIndex, int pageSize)
    {
        Items = items;
        TotalCount = count;
        // Chuẩn hóa: pageno = 0 hoặc 1 đều thành 1
        PageIndex = pageIndex <= 1 ? 1 : pageIndex;
        PageSize = pageSize <= 0 ? PaginationParams.DefaultPageItemSize : pageSize;
    }

    /// <summary>
    /// Tạo danh sách các số trang lân cận để hiển thị trên thanh điều hướng UI.
    /// </summary>
    public IReadOnlyList<int> GetPageNumbers(int maxDisplayPages = 5)
    {
        if (TotalPages <= 1)
        {
            return [1];
        }

        var pages = new List<int>();
        var half = maxDisplayPages / 2;
        var startPage = Math.Max(1, Math.Min(PageIndex, TotalPages) - half);
        var endPage = (int)Math.Min(TotalPages, (long)startPage + maxDisplayPages - 1);

        if (endPage - startPage + 1 < maxDisplayPages)
        {
            startPage = Math.Max(1, endPage - maxDisplayPages + 1);
        }

        for (var i = startPage; i <= endPage; i++)
        {
            pages.Add(i);
        }

        return pages;
    }

    /// <summary>
    /// Chuyển đổi (Project) danh sách thực thể sang danh sách ViewModel/DTO nhưng giữ nguyên metadata phân trang.
    /// </summary>
    public PagedResult<TDestination> Select<TDestination>(Func<T, TDestination> mapper)
    {
        var mappedItems = Items.Select(mapper).ToList();
        return new PagedResult<TDestination>(mappedItems, TotalCount, PageIndex, PageSize);
    }

    public static PagedResult<T> Empty(int pageIndex = 1, int pageSize = PaginationParams.DefaultPageItemSize) =>
        new([], 0, pageIndex, pageSize);
}
