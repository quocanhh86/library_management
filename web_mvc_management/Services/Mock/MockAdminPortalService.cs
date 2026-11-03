using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Helper;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Admin;
using web_mvc_management.Models.ViewModels.Catalog;
using web_mvc_management.Models.ViewModels.Shared;
using web_mvc_management.Util;

namespace web_mvc_management.Services.Mock;

/// <summary>Read-only presentation data for the administration UI; never writes to the database.</summary>
public class MockAdminPortalService(IBookService books, ILibraryInfoService library, IDateTimeProvider clock) : IAdminPortalService
{
    private static readonly (string Name, string Code, string Type)[] People =
    [
        ("Lê Minh Châu", "2021000123", "Sinh viên"), ("Nguyễn Thu Hà", "2022000456", "Sinh viên"),
        ("Trần Quốc Minh", "2020000789", "Giảng viên"), ("Phạm Ngọc Anh", "2023000234", "Sinh viên"),
        ("Vũ Hoàng Nam", "2021000567", "Khách ngoài"), ("Đỗ Thùy Linh", "2022000890", "Sinh viên"),
        ("Bùi Thanh Tùng", "2024000312", "Sinh viên"), ("Hoàng Mai Phương", "2020000645", "Giảng viên")
    ];

    private static string Shelf(BookSummaryViewModel b) => $"KE-{b.CategoryKey.ToUpperInvariant()}-01";
    private static string Date(DateOnly d) => d.ToString(AppConstants.DefaultDateFormat);
    private static string Normalize(string value) => StringUtil.RemoveVietnameseSigns(value).ToLowerInvariant();
    private static AdminRecord Row(string id, StatusKind kind, string status, params (string Key, string Value)[] cells) =>
        new(id, cells.ToDictionary(x => x.Key, x => x.Value), new StatusBadge(kind, status));

    private static IReadOnlyList<AdminLoan> Loans(DateOnly today, IReadOnlyList<BookSummaryViewModel>? catalog = null)
    {
        catalog ??= MockCatalog.Books.OrderByDescending(b => b.PublicationYear).ToList();
        var copyOffsets = new Dictionary<string, (int Start, int Available)>();
        var offset = 1;
        foreach (var book in catalog)
        {
            copyOffsets[book.Title] = (offset, book.AvailableCopies);
            offset += book.TotalCopies;
        }
        var titles = new[] { "Cơ học đất", "Trắc địa đại cương", "Cấu trúc dữ liệu và giải thuật", "Định vị vệ tinh GNSS trong khảo sát công trình", "Quản trị chuỗi cung ứng và logistics", "Giải tích, tập 1", "Kết cấu bê tông cốt thép: phần cấu kiện cơ bản", "Kinh tế vận tải" };
        var readers = new[] { 0, 1, 2, 0, 3, 4, 5, 6 };
        var offsets = new[] { -3, 2, 14, 7, -1, -10, 4, 1 };
        return titles.Select((title, i) => new AdminLoan($"PM-2026-{i + 1:D4}", readers[i].ToString(), People[readers[i]].Name,
            People[readers[i]].Code, title, copyOffsets.TryGetValue(title, out var copy) ? $"BC-{copy.Start + (i == 5 ? 0 : copy.Available):D6}" : "Chưa cập nhật", today.AddDays(-16 + i), today.AddDays(offsets[i]), i == 5)).ToList();
    }

    private static IReadOnlyList<AdminReader> Readers(DateOnly today, decimal fine)
    {
        var loans = Loans(today);
        return People.Select((p, i) => new AdminReader(i.ToString(), p.Name, p.Code, p.Type,
            today.AddDays(i == 4 ? -8 : i is 1 or 6 ? 12 + i : 180 + i),
            i == 3 ? LibraryConstants.CardStatus.Locked : i == 4 ? LibraryConstants.CardStatus.Expired : LibraryConstants.CardStatus.Active,
            loans.Count(l => l.ReaderId == i.ToString() && !l.Returned), p.Type == "Giảng viên" ? 10 : p.Type == "Khách ngoài" ? 2 : 5,
            loans.Where(l => l.ReaderId == i.ToString() && !l.Returned && l.DueOn < today).Sum(l => (today.DayNumber - l.DueOn.DayNumber) * fine))).ToList();
    }

    private static string LoanStatus(AdminLoan loan, DateOnly today) => loan.Returned ? StatusBadge.Loan.Returned : loan.DueOn < today ? StatusBadge.Loan.Overdue : loan.DueOn <= today.AddDays(DueStamp.DueSoonDays) ? StatusBadge.Loan.DueSoon : StatusBadge.Loan.Active;

    public async Task<AdminDashboardViewModel> GetDashboardAsync(CancellationToken ct = default)
    {
        var catalog = await books.GetNewArrivalsAsync(int.MaxValue, ct);
        var info = await library.GetAsync(ct);
        var today = clock.Today;
        var loans = Loans(today, catalog);
        var readers = Readers(today, info.OverdueFinePerDay);
        var overdue = loans.Where(l => !l.Returned && l.DueOn < today).OrderBy(l => l.DueOn).ToList();
        var expiring = readers.Where(r => r.Expiry >= today && r.Expiry <= today.AddDays(30)).ToList();
        return new AdminDashboardViewModel
        {
            Today = today, TotalBooks = catalog.Count, TotalCopies = catalog.Sum(b => b.TotalCopies), AvailableCopies = catalog.Sum(b => b.AvailableCopies),
            Overdue = overdue, RecentLoans = loans.OrderByDescending(l => l.BorrowedOn).Take(5).ToList(), ExpiringReaders = expiring,
            Categories = catalog.GroupBy(b => new { b.CategoryKey, b.CategoryName }).Select(g => new AdminCategoryCount(g.Key.CategoryName, g.Key.CategoryKey, g.Count())).OrderByDescending(c => c.Count).ToList(),
            Metrics =
            [
                new("Phiếu đang mở", loans.Count(l => !l.Returned).ToString(), "Theo dõi hạn trả", "journal-text", ScreenId.CirculationHistory, "status=OPEN"),
                new("Phiếu quá hạn", overdue.Count.ToString(), "Cần nhắc trả tài liệu", "exclamation-circle", ScreenId.CirculationHistory, "status=OVERDUE"),
                new("Thẻ sắp hết hạn", expiring.Count.ToString(), "Trong 30 ngày tới", "person-vcard", ScreenId.ManageReaders, "status=EXPIRING"),
                new("Bản sách sẵn sàng", catalog.Sum(b => b.AvailableCopies).ToString(), $"Trong {catalog.Sum(b => b.TotalCopies)} bản sách", "book", ScreenId.ManageBookCopies, "status=AVAILABLE")
            ]
        };
    }

    public async Task<AdminListViewModel?> GetListAsync(string module, string? keyword, string? status, string? group, int pageNo, int pageItemSize, CancellationToken ct = default)
    {
        var catalog = await books.GetNewArrivalsAsync(int.MaxValue, ct);
        var info = await library.GetAsync(ct);
        var today = clock.Today;
        var rows = new List<AdminRecord>();
        var fields = new List<AdminField>();
        IReadOnlyList<AdminColumn> columns;
        string title, description, create;
        ScreenId screen;
        module = module.ToLowerInvariant();
        var topics = catalog.DistinctBy(b => b.CategoryKey).Select(b => new AdminOption(b.CategoryName, b.CategoryName)).ToList();
        switch (module)
        {
            case "books":
                (title, description, create, screen) = ("Quản lý đầu sách", "Quản lý thông tin thư mục, chuyên ngành và số lượng bản sao của từng đầu sách.", "Thêm đầu sách", ScreenId.ManageBooks);
                columns = [new("code", "Ký hiệu"), new("title", "Tên sách / tác giả"), new("category", "Chuyên ngành"), new("year", "Năm", true), new("copies", "Sẵn sàng / tổng", true)];
                rows = catalog.Select(b => Row(b.Id.ToString(), StatusKind.Copy, b.AvailableCopies > 0 ? "AVAILABLE" : "BORROWED", ("code", b.CallNumber), ("title", b.Title), ("category", b.CategoryName), ("group", b.CategoryName), ("year", b.PublicationYear?.ToString() ?? "—"), ("copies", $"{b.AvailableCopies}/{b.TotalCopies}"), ("authors", b.Authors), ("publisher", b.Publisher), ("isbn", b.Isbn ?? "")) with { Subtitle = b.Authors }).ToList();
                fields = [new("title", "Tên sách"), new("authors", "Tác giả"), new("category", "Chuyên ngành", "select", true, topics), new("publisher", "Nhà xuất bản"), new("year", "Năm xuất bản", "number"), new("isbn", "ISBN", Required: false)];
                break;
            case "bookcopies":
                (title, description, create, screen) = ("Quản lý bản sao", "Mỗi bản sao có mã vạch riêng, vị trí kệ và tình trạng lưu thông.", "Nhập bản sao", ScreenId.ManageBookCopies);
                columns = [new("code", "Mã vạch"), new("title", "Đầu sách"), new("shelf", "Vị trí kệ"), new("category", "Chuyên ngành")];
                var number = 1;
                foreach (var b in catalog)
                    for (var i = 0; i < b.TotalCopies; i++)
                        rows.Add(Row((number++).ToString(), StatusKind.Copy, i < b.AvailableCopies ? "AVAILABLE" : "BORROWED", ("code", $"BC-{number - 1:D6}"), ("title", b.Title), ("shelf", Shelf(b)), ("category", b.CategoryName), ("group", b.CategoryName)));
                fields = [new("code", "Mã vạch"), new("title", "Đầu sách", "select", true, catalog.Select(b => new AdminOption(b.Title, b.Title)).ToList()), new("shelf", "Vị trí kệ"), new("recordStatus", "Tình trạng bản sao", "select", true, [new("AVAILABLE", "Sẵn sàng"), new("BORROWED", "Đang mượn"), new("DAMAGED", "Hỏng"), new("LOST", "Mất"), new("DISPOSED", "Đã thanh lý")])];
                break;
            case "readers":
                (title, description, create, screen) = ("Quản lý độc giả", "Theo dõi thẻ thư viện, hạn thẻ, số sách đang mượn và khoản phạt.", "Thêm độc giả", ScreenId.ManageReaders);
                columns = [new("code", "Mã thẻ"), new("title", "Họ tên độc giả"), new("type", "Loại độc giả"), new("expiry", "Hạn thẻ"), new("borrowed", "Đang mượn", true), new("fine", "Nợ phạt", true)];
                rows = Readers(today, info.OverdueFinePerDay).Select(r => Row(r.Id, StatusKind.Card, r.Status == "ACTIVE" && r.Expiry <= today.AddDays(30) ? "EXPIRING" : r.Status, ("code", r.Code), ("title", r.Name), ("type", r.Type), ("group", r.Type), ("expiry", Date(r.Expiry)), ("borrowed", $"{r.Borrowed}/{r.Limit}"), ("fine", CurrencyHelper.ToVnd(r.Fine)), ("email", $"{r.Code}@example.com"))).ToList();
                fields = [new("title", "Họ và tên"), new("code", "Mã thẻ / mã sinh viên"), new("email", "Email", "email"), new("type", "Loại độc giả", "select", true, info.LoanRules.Select(r => new AdminOption(r.ReaderTypeName, r.ReaderTypeName)).ToList()), new("expiry", "Hạn thẻ", "date"), new("recordStatus", "Trạng thái thẻ", "select", true, [new("ACTIVE", "Còn hạn"), new("LOCKED", "Bị khóa"), new("EXPIRED", "Hết hạn")])];
                break;
            case "history":
                (title, description, create, screen) = ("Phiếu mượn", "Tra cứu lịch sử lưu thông và xử lý các phiếu sắp đến hạn, quá hạn.", "", ScreenId.CirculationHistory);
                columns = [new("code", "Mã phiếu"), new("title", "Tài liệu"), new("reader", "Độc giả"), new("borrowed", "Ngày mượn"), new("due", "Hạn trả")];
                rows = Loans(today, catalog).Select(l => Row(l.Code, StatusKind.Loan, LoanStatus(l, today), ("code", l.Code), ("title", l.BookTitle), ("reader", l.ReaderName), ("borrowed", Date(l.BorrowedOn)), ("due", Date(l.DueOn)), ("barcode", l.Barcode))).ToList();
                break;
            case "fines":
                (title, description, create, screen) = ("Quản lý tiền phạt", "Kiểm tra các khoản phạt dự kiến do trả trễ. Số tiền thực tế cần được xác nhận tại quầy.", "Lập phiếu thu", ScreenId.ManageFines);
                columns = [new("code", "Mã phiếu"), new("title", "Độc giả"), new("book", "Tài liệu quá hạn"), new("days", "Trễ hạn", true), new("amount", "Phạt dự kiến", true)];
                rows = Loans(today, catalog).Where(l => !l.Returned && l.DueOn < today).Select(l => Row(l.Code, StatusKind.Fine, "UNPAID", ("code", l.Code), ("title", l.ReaderName), ("book", l.BookTitle), ("days", $"{today.DayNumber - l.DueOn.DayNumber} ngày"), ("amount", CurrencyHelper.ToVnd((today.DayNumber - l.DueOn.DayNumber) * info.OverdueFinePerDay)))).ToList();
                fields = [new("code", "Mã phiếu mượn"), new("title", "Độc giả"), new("amount", "Số tiền thu (₫)", "number"), new("method", "Phương thức thanh toán", "select", true, [new("CASH", "Tiền mặt"), new("TRANSFER", "Chuyển khoản")])];
                break;
            case "staff":
                (title, description, create, screen) = ("Người dùng & phân quyền", "Tài khoản cán bộ có quyền truy cập khu vực quản trị và quầy thủ thư.", "Thêm người dùng", ScreenId.SystemStaff);
                columns = [new("code", "Tên đăng nhập"), new("title", "Họ và tên"), new("email", "Email"), new("role", "Vai trò")];
                var names = new[] { "Nguyễn Quốc Anh", "Trần Thu Hà", "Lê Minh Đức", "Phạm Thanh Hương" };
                rows = names.Select((name, i) => Row(i.ToString(), StatusKind.Record, i == 2 ? "LOCKED" : i == 3 ? "INACTIVE" : "ACTIVE", ("code", $"canbo{i + 1:D2}"), ("title", name), ("email", $"canbo{i + 1:D2}@example.com"), ("role", i == 0 ? "Quản trị viên" : "Thủ thư"), ("group", i == 0 ? "Quản trị viên" : "Thủ thư"))).ToList();
                fields = [new("title", "Họ và tên"), new("code", "Tên đăng nhập"), new("email", "Email công vụ", "email"), new("role", "Vai trò", "select", true, [new("Thủ thư", "Thủ thư"), new("Quản trị viên", "Quản trị viên")]), new("recordStatus", "Trạng thái tài khoản", "select", true, [new("ACTIVE", "Hoạt động"), new("INACTIVE", "Ngừng hoạt động"), new("LOCKED", "Tạm khóa")])];
                break;
            case "readertypes":
                (title, description, create, screen) = ("Loại độc giả", "Hạn mức mượn và thời hạn trả theo từng nhóm bạn đọc.", "Thêm loại độc giả", ScreenId.ManageReaderTypes);
                columns = [new("code", "Mã loại"), new("title", "Loại độc giả"), new("max", "Hạn mức", true), new("days", "Thời hạn", true)];
                rows = info.LoanRules.Select((r, i) => Row(i.ToString(), StatusKind.Record, "ACTIVE", ("code", i < 3 ? new[] { "STUDENT", "LECTURER", "GUEST" }[i] : $"TYPE-{i + 1}"), ("title", r.ReaderTypeName), ("max", r.MaxBooks.ToString()), ("days", r.LoanDays.ToString()), ("renewals", (i == 0 ? 2 : i == 1 ? 3 : 1).ToString()), ("renewDays", (i == 0 ? 7 : i == 1 ? 14 : 3).ToString()), ("cardMonths", (i == 0 ? 48 : i == 1 ? 60 : 12).ToString()))).ToList();
                fields = [new("code", "Mã loại"), new("title", "Tên loại độc giả"), new("max", "Số sách tối đa", "number"), new("days", "Số ngày mượn", "number"), new("renewals", "Số lần gia hạn tối đa", "number"), new("renewDays", "Số ngày mỗi lần gia hạn", "number"), new("cardMonths", "Hiệu lực thẻ (tháng)", "number")];
                break;
            case "authors":
            case "publishers":
            case "categories":
            case "shelves":
                var authors = module == "authors";
                var publishers = module == "publishers";
                var shelves = module == "shelves";
                (title, description, create, screen) = authors ? ("Quản lý tác giả", "Danh mục tác giả và số đầu sách trong kho.", "Thêm tác giả", ScreenId.ManageAuthors) : publishers ? ("Nhà xuất bản", "Các nhà xuất bản có tài liệu trong danh mục thư viện.", "Thêm nhà xuất bản", ScreenId.ManagePublishers) : shelves ? ("Kệ sách & vị trí", "Tổ chức vị trí xếp giá và theo dõi số bản sách trên từng kệ.", "Thêm kệ sách", ScreenId.ManageShelves) : ("Quản lý thể loại", "Phân loại tài liệu theo chuyên ngành để bạn đọc dễ dàng tra cứu.", "Thêm thể loại", ScreenId.ManageCategories);
                columns = [new("code", "Mã danh mục"), new("title", authors ? "Tên tác giả" : publishers ? "Nhà xuất bản" : shelves ? "Tên kệ / chuyên ngành" : "Tên thể loại"), new("count", shelves ? "Số bản" : "Đầu sách", true)];
                IEnumerable<(string Name, int Count)> values = authors ? catalog.SelectMany(b => b.Authors.Split(',').Select(a => a.Trim())).GroupBy(a => a).Select(g => (g.Key, g.Count())) : publishers ? catalog.GroupBy(b => b.Publisher).Select(g => (g.Key, g.Count())) : catalog.GroupBy(b => b.CategoryName).Select(g => (g.Key, shelves ? g.Sum(b => b.TotalCopies) : g.Count()));
                rows = values.Select((v, i) => Row(i.ToString(), StatusKind.Record, "ACTIVE", ("code", shelves ? Shelf(catalog.First(b => b.CategoryName == v.Name)) : $"{(authors ? "TG" : publishers ? "NXB" : "TL")}-{i + 1:D3}"), ("title", v.Name), ("count", v.Count.ToString()))).ToList();
                fields = [new("code", "Mã danh mục"), new("title", authors ? "Tên tác giả" : publishers ? "Tên nhà xuất bản" : shelves ? "Tên kệ / vị trí" : "Tên thể loại")];
                break;
            default: return null;
        }
        var count = rows.Count;
        var statuses = rows.Select(r => new AdminOption(r.Badge.Code, r.Badge.Resolve().Label)).DistinctBy(o => o.Value).ToList();
        if (module == "history") statuses.Insert(0, new AdminOption("OPEN", "Chưa trả"));
        var groups = rows.Select(r => r.Cells.GetValueOrDefault("group")).Where(g => !string.IsNullOrWhiteSpace(g)).Distinct().Select(g => new AdminOption(g!, g!)).ToList();
        var term = Normalize(keyword?.Trim() ?? "");
        rows = rows.Where(r => (term.Length == 0 || Normalize(string.Join(" ", r.Cells.Values)).Contains(term)) && (string.IsNullOrEmpty(status) || (module == "history" && status == "OPEN" ? r.Badge.Code != StatusBadge.Loan.Returned : r.Badge.Code == status)) && (string.IsNullOrEmpty(group) || r.Cells.GetValueOrDefault("group") == group)).ToList();
        var size = AppConstants.AllowedPageSizes.Contains(pageItemSize) ? pageItemSize : 10;
        var page = Math.Clamp(pageNo, 1, Math.Max(1, (int)Math.Ceiling(rows.Count / (double)size)));
        return new AdminListViewModel { Module = module, Title = title, Description = description, CreateLabel = create, Screen = screen, Columns = columns, Fields = fields, Statuses = statuses, Groups = groups, UnfilteredCount = count, Keyword = keyword, Status = status, Group = group, Results = new PagedResult<AdminRecord>(rows.Skip((page - 1) * size).Take(size).ToList(), rows.Count, page, size) };
    }

    public async Task<AdminCirculationViewModel> GetCirculationAsync(bool returning, string? card, CancellationToken ct = default)
    {
        var info = await library.GetAsync(ct);
        var catalog = await books.GetNewArrivalsAsync(int.MaxValue, ct);
        var readers = Readers(clock.Today, info.OverdueFinePerDay);
        var reader = string.IsNullOrWhiteSpace(card) ? null : readers.FirstOrDefault(r => r.Code == card.Trim());
        return new AdminCirculationViewModel
        {
            Returning = returning, Today = clock.Today, Card = card, Reader = reader, Readers = readers,
            Error = !string.IsNullOrWhiteSpace(card) && reader is null ? "Không tìm thấy thẻ trong dữ liệu mẫu. Chọn một thẻ gợi ý để xem thử." : null,
            Books = catalog, Loans = reader is null ? [] : Loans(clock.Today, catalog).Where(l => l.ReaderId == reader.Id && !l.Returned).ToList(),
            FinePerDay = info.OverdueFinePerDay, LoanDays = info.LoanRules.FirstOrDefault(r => r.ReaderTypeName == reader?.Type)?.LoanDays ?? 14
        };
    }

    public async Task<AdminSettingsViewModel> GetSettingsAsync(CancellationToken ct = default) => new()
    {
        Info = await library.GetAsync(ct), DamagedFinePercent = 50, LostFinePercent = 150, MaxUnpaidFine = 50000m, ReservationHoldHours = 48
    };
}
