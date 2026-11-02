using web_mvc_management.Models.ViewModels.Home;

namespace web_mvc_management.Services;

public interface IBorrowService
{
    Task<ReaderLoansViewModel> GetReaderLoansAsync(Guid readerId, CancellationToken ct = default);
}
