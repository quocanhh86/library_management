namespace web_mvc_management.Models.ViewModels.Home;

public class ReaderLoansViewModel
{
    public IReadOnlyList<CurrentLoanViewModel> Loans { get; init; } = [];
    public decimal UnpaidFines { get; init; }
}

public record CurrentLoanViewModel(string Title, string Authors, DateOnly DueDate);
