namespace web_mvc_management.Helper;

/// <summary>
/// Hỗ trợ tính toán danh sách số trang hiển thị trên thanh phân trang Razor View kèm dấu chấm lửng (...).
/// </summary>
public static class PaginationHelper
{
    /// <summary>
    /// Tạo danh sách các số trang hiển thị (null đại diện cho dấu '...').
    /// Ví dụ: 10 trang, đang ở trang 5 -> [1, null, 4, 5, 6, null, 10]
    /// </summary>
    public static List<int?> GetVisiblePages(int currentPage, int totalPages, int delta = 2)
    {
        if (totalPages <= 1)
        {
            return [1];
        }

        var pages = new List<int?>();
        var left = Math.Max(1, currentPage - delta);
        var right = Math.Min(totalPages, currentPage + delta);

        if (left > 1)
        {
            pages.Add(1);
            if (left > 2)
            {
                pages.Add(null);
            }
        }

        for (var i = left; i <= right; i++)
        {
            pages.Add(i);
        }

        if (right < totalPages)
        {
            if (right < totalPages - 1)
            {
                pages.Add(null);
            }
            pages.Add(totalPages);
        }

        return pages;
    }
}

