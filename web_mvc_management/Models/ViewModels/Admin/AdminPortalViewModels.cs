using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Models.ViewModels.Catalog;
using web_mvc_management.Models.ViewModels.Home;
using web_mvc_management.Models.ViewModels.Shared;

namespace web_mvc_management.Models.ViewModels.Admin;

public record AdminColumn(string Key, string Label, bool Numeric = false);
public record AdminOption(string Value, string Label);
public record AdminField(string Key, string Label, string Type = "text", bool Required = true, IReadOnlyList<AdminOption>? Options = null);
public record AdminRecord(string Id, IReadOnlyDictionary<string, string> Cells, StatusBadge Badge, string? Subtitle = null);
public record AdminReader(string Id, string Name, string Code, string Type, DateOnly Expiry, string Status, int Borrowed, int Limit, decimal Fine);
public record AdminLoan(string Code, string ReaderId, string ReaderName, string CardNo, string BookTitle, string Barcode, DateOnly BorrowedOn, DateOnly DueOn, bool Returned = false);
public record AdminMetric(string Label, string Value, string Note, string Icon, ScreenId Destination, string? Filter = null);
public record AdminCategoryCount(string Name, string Key, int Count);

public class AdminListViewModel
{
    public string Module { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public ScreenId Screen { get; init; }
    public string CreateLabel { get; init; } = "";
    public IReadOnlyList<AdminColumn> Columns { get; init; } = [];
    public IReadOnlyList<AdminField> Fields { get; init; } = [];
    public PagedResult<AdminRecord> Results { get; init; } = new();
    public IReadOnlyList<AdminOption> Statuses { get; init; } = [];
    public IReadOnlyList<AdminOption> Groups { get; init; } = [];
    public string? Keyword { get; init; }
    public string? Status { get; init; }
    public string? Group { get; init; }
    public int UnfilteredCount { get; init; }
}

public class AdminDashboardViewModel
{
    public DateOnly Today { get; init; }
    public IReadOnlyList<AdminMetric> Metrics { get; init; } = [];
    public IReadOnlyList<AdminLoan> Overdue { get; init; } = [];
    public IReadOnlyList<AdminLoan> RecentLoans { get; init; } = [];
    public IReadOnlyList<AdminReader> ExpiringReaders { get; init; } = [];
    public IReadOnlyList<AdminCategoryCount> Categories { get; init; } = [];
    public int TotalBooks { get; init; }
    public int TotalCopies { get; init; }
    public int AvailableCopies { get; init; }
}

public class AdminCirculationViewModel
{
    public bool Returning { get; init; }
    public DateOnly Today { get; init; }
    public string? Card { get; init; }
    public string? Error { get; init; }
    public AdminReader? Reader { get; init; }
    public IReadOnlyList<AdminReader> Readers { get; init; } = [];
    public IReadOnlyList<BookSummaryViewModel> Books { get; init; } = [];
    public IReadOnlyList<AdminLoan> Loans { get; init; } = [];
    public decimal FinePerDay { get; init; }
    public int LoanDays { get; init; }
    public bool CanBorrow => Reader is { Status: LibraryConstants.CardStatus.Active } reader && reader.Expiry >= Today && reader.Borrowed < reader.Limit && reader.Fine == 0;
}

public class AdminSettingsViewModel
{
    public LibraryInfoViewModel Info { get; init; } = new();
    public short DamagedFinePercent { get; init; }
    public short LostFinePercent { get; init; }
    public decimal MaxUnpaidFine { get; init; }
    public short ReservationHoldHours { get; init; }
}

public static class AdminNavigation
{
    public static string Path(ScreenId id, bool preview, string? role = null, string? query = null)
    {
        var path = (preview ? "/dev" : "") + Screens.GetPath(id);
        var parts = new List<string>();
        if (preview) parts.Add("role=" + Uri.EscapeDataString(role == AppRoles.Librarian ? AppRoles.Librarian : AppRoles.Admin));
        if (!string.IsNullOrWhiteSpace(query)) parts.Add(query);
        return path + (parts.Count > 0 ? "?" + string.Join("&", parts) : "");
    }
}
