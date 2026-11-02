using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Home;

namespace web_mvc_management.Services.Mock;

/// <summary>
/// Hai cuốn đang mượn: một cuốn trễ 3 ngày, một cuốn còn hạn, để thấy đủ trạng thái con dấu.
/// </summary>
public class MockBorrowService(IDateTimeProvider dateTimeProvider) : IBorrowService
{
    public Task<ReaderLoansViewModel> GetReaderLoansAsync(Guid readerId, CancellationToken ct = default)
    {
        var today = dateTimeProvider.Today;
        return Task.FromResult(new ReaderLoansViewModel
        {
            Loans =
            [
                new("Cơ học đất", "Vũ Hồng Sơn", today.AddDays(-3)),
                new("Trắc địa đại cương", "Đinh Công Tuấn", today.AddDays(14)),
            ],
            UnpaidFines = 6000m
        });
    }
}
