namespace web_mvc_management.Models.ViewModels.SystemAdmin;

public class DashboardViewModel
{
    // Tổng quan

    public int TotalReaders { get; set; }
    public int TotalBooks { get; set; }
    public int TotalBookCopies { get; set; }
    public int BorrowingBooks { get; set; }
    public int TotalBorrowSlips { get; set; }
    public decimal TotalFines { get; set; }
    public int SelectedYear { get; set; }

    // Thống kê theo tháng
    public IReadOnlyList<DashboardMonthlyItemViewModel> MonthlyStatistics { get; set; } = [];

    // Top sách
    public IReadOnlyList<DashboardBookRankingViewModel> MostBorrowedBooks { get; set; } = [];

    // Top thể loại
    public IReadOnlyList<DashboardCategoryRankingViewModel> MostBorrowedCategories { get; set; } = [];

    // Top độc giả
    public IReadOnlyList<DashboardReaderRankingViewModel> MostActiveReaders { get; set; } = [];

    // Sách ít được mượn
    public IReadOnlyList<DashboardBookRankingViewModel> LeastBorrowedBooks { get; set; } = [];
}

public class DashboardMonthlyItemViewModel
{
    public int MonthNumber { get; set; }
    public string Month { get; set; } = string.Empty;
    public int BorrowCount { get; set; }
}

public class DashboardBookRankingViewModel
{
    public Guid BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public int BorrowCount { get; set; }
}

public class DashboardCategoryRankingViewModel
{
    public Guid? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int BorrowCount { get; set; }
}

public class DashboardReaderRankingViewModel
{
    public Guid ReaderId { get; set; }
    public string ReaderName { get; set; } = string.Empty;
    public int BorrowCount { get; set; }
}