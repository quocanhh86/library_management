using web_mvc_management.Models.ViewModels.Catalog;

namespace web_mvc_management.Models.ViewModels.Home;

public class HomeViewModel
{
    public IReadOnlyList<BookSummaryViewModel> NewArrivals { get; init; } = [];
    public IReadOnlyList<TopicViewModel> Topics { get; init; } = [];
    public LibraryInfoViewModel Info { get; init; } = new();

    /// <summary>Chỉ có khi người xem là độc giả đã đăng nhập.</summary>
    public ReaderLoansViewModel? ReaderLoans { get; init; }

    public DateOnly Today { get; init; }
}

/// <summary>Thể loại hiện dưới ô tìm kiếm; Key dùng làm tham số lọc ở trang tra cứu.</summary>
public record TopicViewModel(string Key, string Name);
