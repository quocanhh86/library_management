using web_mvc_management.Models.ViewModels.Home;

namespace web_mvc_management.Services.Mock;

/// <summary>
/// Quy định mượn lấy đúng số đang seed trong DbInitializer (loại độc giả, LibrarySetting).
/// Giờ mở cửa và liên hệ để null: chưa có số liệu thật, View hiện TODO(xác nhận).
/// </summary>
public class MockLibraryInfoService : ILibraryInfoService
{
    public Task<LibraryInfoViewModel> GetAsync(CancellationToken ct = default) => Task.FromResult(new LibraryInfoViewModel
    {
        LoanRules =
        [
            new("Sinh viên", 5, 14),
            new("Giảng viên", 10, 30),
            new("Khách ngoài", 2, 7),
        ],
        OverdueFinePerDay = 2000m
    });
}
