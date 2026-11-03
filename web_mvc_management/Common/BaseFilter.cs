namespace web_mvc_management.Common;

/// <summary>
/// Lớp cơ sở chứa các tham số tìm kiếm, lọc và phân trang dùng chung.
/// Kế thừa PaginationParams với mặc định pageItemSize = 10 và hỗ trợ pageNo = 0 hoặc 1.
/// </summary>
public class BaseFilter : PaginationParams
{
    private string? _keyword;

    public string? Keyword
    {
        get => _keyword;
        set => _keyword = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public string? Status { get; set; }

    public string? SortBy { get; set; }

    public bool IsAscending { get; set; } = false;
}
