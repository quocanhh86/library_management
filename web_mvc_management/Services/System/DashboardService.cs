using Microsoft.EntityFrameworkCore;
using web_mvc_management.Constant;
using web_mvc_management.Helper;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Repositories;

namespace web_mvc_management.Services;

public class DashboardService(
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider) : IDashboardService
{
    public async Task<DashboardViewModel> GetDashboardAsync(int year, CancellationToken ct = default)
    {
        var offset = dateTimeProvider.Now.Offset;
        var yearStart = DateTimeHelper.UtcDayBoundary(new DateOnly(year, 1, 1), offset);
        var yearEnd = DateTimeHelper.UtcDayBoundary(new DateOnly(year, 12, 31), offset, endOfDay: true);

        var readersQuery = unitOfWork.Readers.GetQueryable(asNoTracking: true)
            .Where(x => x.Status != EntityStatus.Deleted);

        var booksQuery = unitOfWork.Books.GetQueryable(asNoTracking: true)
            .Where(x => x.Status != EntityStatus.Deleted);

        var copiesQuery = unitOfWork.BookCopies.GetQueryable(asNoTracking: true)
            .Where(x => x.Status != EntityStatus.Deleted && x.Book.Status != EntityStatus.Deleted);

        var slipsQuery = unitOfWork.BorrowSlips.GetQueryable(asNoTracking: true)
            .Where(x => x.Status != EntityStatus.Deleted);

        var detailsQuery = unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .Where(x => x.Status != EntityStatus.Deleted && x.BorrowSlip.Status != EntityStatus.Deleted);

        var finesQuery = unitOfWork.Fines.GetQueryable(asNoTracking: true)
            .Where(x => x.Status != EntityStatus.Deleted
                        && x.FineStatus != LibraryConstants.FineStatus.Cancelled
                        && x.BorrowDetail.Status != EntityStatus.Deleted
                        && x.BorrowDetail.BorrowSlip.Status != EntityStatus.Deleted);

        var totalReaders = await readersQuery.CountAsync(ct);
        var totalBooks = await booksQuery.CountAsync(ct);
        var totalBookCopies = await copiesQuery.CountAsync(ct);
        var borrowingBooks = await detailsQuery.CountAsync(x => x.ReturnedAt == null, ct);
        var totalBorrowSlips = await slipsQuery.CountAsync(ct);
        var totalFines = await finesQuery.SumAsync(x => x.Amount, ct);

        var slipDatesInYear = await slipsQuery
            .Where(x => x.BorrowedAt >= yearStart && x.BorrowedAt <= yearEnd)
            .Select(x => x.BorrowedAt)
            .ToListAsync(ct);

        var monthlyCounts = slipDatesInYear
            .GroupBy(dt => dt.ToOffset(offset).Month)
            .ToDictionary(g => g.Key, g => g.Count());

        var monthlyStatistics = Enumerable.Range(1, 12)
            .Select(month => new DashboardMonthlyItemViewModel
            {
                MonthNumber = month,
                Month = $"Tháng {month}",
                BorrowCount = monthlyCounts.GetValueOrDefault(month, 0)
            })
            .ToList();

        var mostBorrowedBooks = await detailsQuery
            .Where(x => x.BorrowSlip.BorrowedAt >= yearStart
                        && x.BorrowSlip.BorrowedAt <= yearEnd
                        && x.BookCopy.Book.Status != EntityStatus.Deleted)
            .GroupBy(x => new { x.BookCopy.BookId, x.BookCopy.Book.Title })
            .Select(g => new DashboardBookRankingViewModel
            {
                BookId = g.Key.BookId,
                BookTitle = g.Key.Title,
                BorrowCount = g.Count()
            })
            .OrderByDescending(x => x.BorrowCount)
            .ThenBy(x => x.BookTitle)
            .Take(5)
            .ToListAsync(ct);

        var leastBorrowedBooks = await booksQuery
            .Select(x => new DashboardBookRankingViewModel
            {
                BookId = x.Id,
                BookTitle = x.Title,
                BorrowCount = x.BookCopies
                    .Where(c => c.Status != EntityStatus.Deleted)
                    .SelectMany(c => c.BorrowDetails)
                    .Count(d => d.Status != EntityStatus.Deleted
                                && d.BorrowSlip.Status != EntityStatus.Deleted
                                && d.BorrowSlip.BorrowedAt >= yearStart
                                && d.BorrowSlip.BorrowedAt <= yearEnd)
            })
            .OrderBy(x => x.BorrowCount)
            .ThenBy(x => x.BookTitle)
            .Take(5)
            .ToListAsync(ct);

        var mostBorrowedCategories = await detailsQuery
            .Where(x => x.BorrowSlip.BorrowedAt >= yearStart
                        && x.BorrowSlip.BorrowedAt <= yearEnd
                        && x.BookCopy.Book.CategoryId != null
                        && x.BookCopy.Book.Category!.Status != EntityStatus.Deleted)
            .GroupBy(x => new { x.BookCopy.Book.CategoryId, x.BookCopy.Book.Category!.Name })
            .Select(g => new DashboardCategoryRankingViewModel
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.Name,
                BorrowCount = g.Count()
            })
            .OrderByDescending(x => x.BorrowCount)
            .ThenBy(x => x.CategoryName)
            .Take(5)
            .ToListAsync(ct);

        var mostActiveReaders = await detailsQuery
            .Where(x => x.BorrowSlip.BorrowedAt >= yearStart
                        && x.BorrowSlip.BorrowedAt <= yearEnd
                        && x.BorrowSlip.Reader.Status != EntityStatus.Deleted)
            .GroupBy(x => new { x.BorrowSlip.ReaderId, x.BorrowSlip.Reader.FullName })
            .Select(g => new DashboardReaderRankingViewModel
            {
                ReaderId = g.Key.ReaderId,
                ReaderName = g.Key.FullName,
                BorrowCount = g.Count()
            })
            .OrderByDescending(x => x.BorrowCount)
            .ThenBy(x => x.ReaderName)
            .Take(5)
            .ToListAsync(ct);

        return new DashboardViewModel
        {
            TotalReaders = totalReaders,
            TotalBooks = totalBooks,
            TotalBookCopies = totalBookCopies,
            BorrowingBooks = borrowingBooks,
            TotalBorrowSlips = totalBorrowSlips,
            TotalFines = totalFines,
            SelectedYear = year,
            MonthlyStatistics = monthlyStatistics,
            MostBorrowedBooks = mostBorrowedBooks,
            LeastBorrowedBooks = leastBorrowedBooks,
            MostBorrowedCategories = mostBorrowedCategories,
            MostActiveReaders = mostActiveReaders
        };
    }
}
