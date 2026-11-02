using web_mvc_management.Models.ViewModels.Catalog;
using web_mvc_management.Models.ViewModels.Home;
using web_mvc_management.Util;

namespace web_mvc_management.Services.Mock;

/// <summary>
/// Dữ liệu mẫu cho giai đoạn dựng UI. Thay bằng service EF Core khi nối DB, View không đổi.
/// </summary>
public static class MockCatalog
{
    private static BookSummaryViewModel B(int n, string title, string authors, string publisher, short year,
        string call, string category, string key, int available, int total, DateOnly? earliestDue = null) => new()
    {
        Id = new Guid($"00000000-0000-0000-0000-{n:D12}"),
        Title = title,
        Authors = authors,
        Publisher = publisher,
        PublicationYear = year,
        CallNumber = call,
        CategoryName = category,
        CategoryKey = key,
        AvailableCopies = available,
        TotalCopies = total,
        EarliestDueDate = earliestDue
    };

    // TODO(xác nhận): danh sách thể loại thật; tên ngắn vì hiện trên bìa chữ 56px và dải tìm kiếm
    private const string Civil = "Cầu đường";
    private const string Logistics = "Kinh tế vận tải";
    private const string It = "Công nghệ thông tin";
    private const string Science = "Toán học";
    private const string Survey = "Trắc địa";
    private const string Humanities = "Kỹ năng học tập";

    public static readonly IReadOnlyList<BookSummaryViewModel> Books =
    [
        B(1, "Kết cấu bê tông cốt thép: phần cấu kiện cơ bản", "Phạm Văn Hội, Trịnh Minh Khoa", "NXB Xây dựng", 2019, "624.18 PH-H", Civil, "civil", 3, 5),
        B(2, "Thiết kế cầu bê tông cốt thép", "Đỗ Quang Vinh", "NXB Giao thông Vận tải", 2016, "624.2 ĐO-V", Civil, "civil", 0, 4, new DateOnly(2026, 11, 18)),
        B(3, "Thiết kế đường ô tô, tập 1", "Lương Đức Thịnh, Hoàng Thu Trang", "NXB Giao thông Vận tải", 2020, "625.7 LU-T", Civil, "civil", 6, 8),
        B(4, "Cơ học đất", "Vũ Hồng Sơn", "NXB Xây dựng", 2018, "624.151 VU-S", Civil, "civil", 2, 6),
        B(5, "Nền và móng công trình giao thông", "Nguyễn Thị Hạnh, Tạ Văn Long", "NXB Giao thông Vận tải", 2021, "624.15 NG-H", Civil, "civil", 1, 3),
        B(6, "Trắc địa đại cương", "Đinh Công Tuấn", "NXB Giáo dục Việt Nam", 2017, "526.9 ĐI-T", Survey, "survey", 9, 12),
        B(7, "Định vị vệ tinh GNSS trong khảo sát công trình", "Bùi Thanh Hải", "NXB Khoa học và Kỹ thuật", 2022, "526.98 BU-H", Survey, "survey", 1, 2),
        B(8, "Kinh tế vận tải", "Hà Minh Châu", "NXB Giao thông Vận tải", 2019, "388.049 HA-C", Logistics, "logistics", 4, 7),
        B(9, "Quản trị chuỗi cung ứng và logistics", "Trần Quốc Việt, Lê Thị Ngọc", "NXB Thông tin và Truyền thông", 2023, "658.78 TR-V", Logistics, "logistics", 0, 3, new DateOnly(2026, 11, 9)),
        B(10, "Kinh tế xây dựng công trình giao thông", "Phan Đình Khôi", "NXB Xây dựng", 2020, "338.4762 PH-K", Logistics, "logistics", 5, 5),
        B(11, "Cấu trúc dữ liệu và giải thuật", "Ngô Đức Anh", "NXB Thông tin và Truyền thông", 2021, "005.73 NG-A", It, "it", 2, 10),
        B(12, "Giáo trình thiết kế cơ sở dữ liệu và SQL Server", "Lê Văn Thắng", "NXB Giáo dục Việt Nam", 2022, "005.756 LE-T", It, "it", 7, 9),
        B(13, "Giải tích, tập 1", "Hoàng Văn Phúc", "NXB Giáo dục Việt Nam", 2018, "515 HO-P", Science, "science", 14, 20),
        B(14, "Xác suất thống kê ứng dụng trong kỹ thuật", "Trương Thị Lan", "NXB Khoa học và Kỹ thuật", 2019, "519.5 TR-L", Science, "science", 3, 8),
        B(15, "Phương pháp học tập ở bậc đại học", "Mai Thanh Bình", "NXB Trẻ", 2024, "378.17 MA-B", Humanities, "humanities", 2, 2),
    ];
}

public class MockBookService : IBookService
{
    public Task<IReadOnlyList<BookSummaryViewModel>> GetNewArrivalsAsync(int count, CancellationToken ct = default)
    {
        // mock lấy theo năm xuất bản; bản EF lấy theo ngày nhập bản sao (book_copies.acquired_date)
        IReadOnlyList<BookSummaryViewModel> items = MockCatalog.Books
            .OrderByDescending(b => b.PublicationYear)
            .Take(count)
            .ToList();
        return Task.FromResult(items);
    }

    public Task<IReadOnlyList<TopicViewModel>> GetTopicsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<TopicViewModel> topics = MockCatalog.Books
            .DistinctBy(b => b.CategoryKey)
            .Select(b => new TopicViewModel(b.CategoryKey, b.CategoryName))
            .ToList();
        return Task.FromResult(topics);
    }

    public Task<IReadOnlyList<BookSummaryViewModel>> SuggestAsync(string keyword, int limit = 6, CancellationToken ct = default)
    {
        var term = StringUtil.RemoveVietnameseSigns(keyword.Trim()).ToLowerInvariant();
        if (term.Length < 2)
        {
            return Task.FromResult<IReadOnlyList<BookSummaryViewModel>>([]);
        }

        IReadOnlyList<BookSummaryViewModel> items = MockCatalog.Books
            .Where(b => StringUtil.RemoveVietnameseSigns($"{b.Title} {b.Authors}").ToLowerInvariant().Contains(term))
            .Take(limit)
            .ToList();
        return Task.FromResult(items);
    }
}
