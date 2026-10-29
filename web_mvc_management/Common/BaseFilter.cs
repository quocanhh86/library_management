namespace web_mvc_management.Common;

/// <summary>
/// Lớp cơ sở chứa các tham số tìm kiếm, lọc và phân trang dùng chung.
/// </summary>
public class BaseFilter
{
    private int _pageIndex = 1;
    private int _pageSize = 10;
    private string? _keyword;

    public int PageIndex
    {
        get => _pageIndex;
        set => _pageIndex = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 10 : (value > 100 ? 100 : value);
    }

    public string? Keyword
    {
        get => _keyword;
        set => _keyword = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public string? Status { get; set; }

    public string? SortBy { get; set; }

    public bool IsAscending { get; set; } = false;
}

