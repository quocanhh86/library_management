using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;

namespace web_mvc_management.Extensions;

/// <summary>
/// Các phương thức mở rộng hỗ trợ phân trang LINQ / EF Core dùng chung.
/// - pageItemSize mặc định = 10 (tùy chọn theo client, nếu không truyền hoặc &lt;= 0 thì mặc định 10).
/// - pageNo = 0 hoặc 1 đều truy vấn từ item index 0 trong CSDL (Skip = 0).
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Thực thi truy vấn phân trang bất đồng bộ từ IQueryable (EF Core).
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int pageNo = 1,
        int pageItemSize = PaginationParams.DefaultPageItemSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var safePageItemSize = pageItemSize <= 0 ? PaginationParams.DefaultPageItemSize : pageItemSize;
        var normalizedPageNo = pageNo <= 1 ? 1 : pageNo;
        var skip = (normalizedPageNo - 1) * safePageItemSize;

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip(skip).Take(safePageItemSize).ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalCount, normalizedPageNo, safePageItemSize);
    }

    /// <summary>
    /// Thực thi truy vấn phân trang từ đối tượng tham số PaginationParams.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PaginationParams pagination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(pagination);

        return await query.ToPagedResultAsync(pagination.PageNo, pagination.PageItemSize, cancellationToken);
    }

    /// <summary>
    /// Phân trang đồng bộ cho danh sách in-memory (IEnumerable).
    /// </summary>
    public static PagedResult<T> ToPagedResult<T>(
        this IEnumerable<T> source,
        int pageNo = 1,
        int pageItemSize = PaginationParams.DefaultPageItemSize)
    {
        ArgumentNullException.ThrowIfNull(source);

        var safePageItemSize = pageItemSize <= 0 ? PaginationParams.DefaultPageItemSize : pageItemSize;
        var normalizedPageNo = pageNo <= 1 ? 1 : pageNo;
        var skip = (normalizedPageNo - 1) * safePageItemSize;

        var list = source as IList<T> ?? [.. source];
        var totalCount = list.Count;
        var items = list.Skip(skip).Take(safePageItemSize).ToList();

        return new PagedResult<T>(items, totalCount, normalizedPageNo, safePageItemSize);
    }

    /// <summary>
    /// Phân trang đồng bộ cho danh sách in-memory từ đối tượng PaginationParams.
    /// </summary>
    public static PagedResult<T> ToPagedResult<T>(
        this IEnumerable<T> source,
        PaginationParams pagination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(pagination);

        return source.ToPagedResult(pagination.PageNo, pagination.PageItemSize);
    }
}

