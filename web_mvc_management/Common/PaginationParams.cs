using System;

namespace web_mvc_management.Common;

/// <summary>
/// Lớp chứa tham số phân trang chuẩn dùng chung cho toàn bộ hệ thống ASP.NET Core MVC.
/// - pageItemSize: Mặc định là 10 (cho phép client tùy chọn qua query string, nếu không truyền hoặc <= 0 thì mặc định 10, tối đa 100).
/// - pageNo: Hỗ trợ client truyền 0 hoặc 1, cả hai đều truy vấn từ bản ghi đầu tiên trong CSDL (Skip = 0).
/// </summary>
public class PaginationParams
{
    public const int DefaultPageItemSize = 10;
    public const int MaxPageItemSize = 100;

    private int _pageNo = 1;
    private int _pageItemSize = DefaultPageItemSize;

    /// <summary>
    /// Số thứ tự trang nhận từ client (hỗ trợ cả 0 và 1 đều lấy từ bản ghi đầu tiên).
    /// </summary>
    public int PageNo
    {
        get => _pageNo;
        set => _pageNo = value;
    }

    /// <summary>
    /// Alias của PageNo (cho phép model binding nhận tham số `pageIndex`, `pageNo` hoặc `page`).
    /// </summary>
    public int PageIndex
    {
        get => PageNo;
        set => PageNo = value;
    }

    /// <summary>
    /// Số lượng bản ghi trên một trang (mặc định 10).
    /// Cho phép client tùy chọn (ví dụ 10, 20, 50, 100).
    /// Nếu client không truyền hoặc <= 0 thì tự động gán mặc định 10.
    /// </summary>
    public int PageItemSize
    {
        get => _pageItemSize;
        set => _pageItemSize = value <= 0 ? DefaultPageItemSize : (value > MaxPageItemSize ? MaxPageItemSize : value);
    }

    /// <summary>
    /// Alias của PageItemSize (cho phép model binding nhận tham số `pageSize` hoặc `pageItemSize`).
    /// </summary>
    public int PageSize
    {
        get => PageItemSize;
        set => PageItemSize = value;
    }

    /// <summary>
    /// Số thứ tự trang được chuẩn hóa cho việc hiển thị giao diện UI (luôn >= 1).
    /// pageno = 0 hoặc 1 đều được chuẩn hóa thành 1.
    /// </summary>
    public int NormalizedPageNo => PageNo <= 1 ? 1 : PageNo;

    /// <summary>
    /// Số bản ghi cần bỏ qua khi truy vấn Database (Skip offset).
    /// - Client truyền pageno = 0: Skip = (1 - 1) * 10 = 0 (truy vấn từ item 0 trong CSDL).
    /// - Client truyền pageno = 1: Skip = (1 - 1) * 10 = 0 (truy vấn từ item 0 trong CSDL).
    /// - Client truyền pageno = 2: Skip = (2 - 1) * 10 = 10.
    /// - Client truyền pageno = 3: Skip = (3 - 1) * 10 = 20.
    /// </summary>
    public int SkipCount => (NormalizedPageNo - 1) * PageItemSize;

    /// <summary>
    /// Số bản ghi cần lấy (Take limit).
    /// </summary>
    public int TakeCount => PageItemSize;
}
