namespace web_mvc_management.Models.ViewModels.Home;

/// <summary>
/// Thông tin thực dụng ở cột phụ trang chủ. Trường null nghĩa là chưa có số liệu thật: View hiện TODO(xác nhận).
/// </summary>
public class LibraryInfoViewModel
{
    public string? OpeningHours { get; init; }
    public string? Contact { get; init; }
    public IReadOnlyList<LoanRuleViewModel> LoanRules { get; init; } = [];
    public decimal OverdueFinePerDay { get; init; }
}

public record LoanRuleViewModel(string ReaderTypeName, int MaxBooks, int LoanDays);
