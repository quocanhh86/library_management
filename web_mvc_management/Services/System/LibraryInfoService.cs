using Microsoft.EntityFrameworkCore;
using web_mvc_management.Constant;
using web_mvc_management.Models.ViewModels.Home;
using web_mvc_management.Repositories;

namespace web_mvc_management.Services;

/// <summary>
/// Lấy thông tin quy định mượn trả trực tiếp từ cơ sở dữ liệu (ReaderTypes và LibrarySettings).
/// </summary>
public class LibraryInfoService(IUnitOfWork unitOfWork) : ILibraryInfoService
{
    public async Task<LibraryInfoViewModel> GetAsync(CancellationToken ct = default)
    {
        var readerTypes = await unitOfWork.ReaderTypes.GetQueryable(asNoTracking: true)
            .Where(rt => rt.Status == EntityStatus.Active)
            .OrderByDescending(rt => rt.MaxBorrowBooks)
            .Select(rt => new LoanRuleViewModel(rt.Name, rt.MaxBorrowBooks, rt.LoanDays))
            .ToListAsync(ct);

        var setting = await unitOfWork.LibrarySettings.GetQueryable(asNoTracking: true)
            .Where(s => s.Status == EntityStatus.Active)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (readerTypes.Count == 0)
        {
            readerTypes =
            [
                new("Sinh viên", 5, 14),
                new("Giảng viên", 10, 30),
                new("Khách ngoài", 2, 7),
            ];
        }

        return new LibraryInfoViewModel
        {
            LoanRules = readerTypes,
            OverdueFinePerDay = setting?.OverdueFinePerDay ?? LibraryConstants.Defaults.OverdueFinePerDay
        };
    }
}
