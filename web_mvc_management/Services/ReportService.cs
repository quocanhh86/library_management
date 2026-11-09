using System.Text;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Infrastructure;
using web_mvc_management.Helper;
using web_mvc_management.Models.ViewModels.Reports;
using web_mvc_management.Repositories;
using static web_mvc_management.Constant.LibraryConstants;

namespace web_mvc_management.Services;

public class ReportService(
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider) : IReportService
{
    public async Task<ReportDashboardViewModel> GetReportDashboardAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var today = dateTimeProvider.Today;
        var (rangeStart, rangeEnd) = ResolveDateRange(filter, today);

        var localOffset = dateTimeProvider.Now.Offset;
        var fromOffset = DateTimeHelper.UtcDayBoundary(rangeStart, localOffset);
        var toOffset = DateTimeHelper.UtcDayBoundary(rangeEnd, localOffset, endOfDay: true);

        var setting = await unitOfWork.LibrarySettings.GetQueryable(asNoTracking: true)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        var finePerDay = setting?.OverdueFinePerDay ?? 5000m;

        // 1. Slips & Details in range
        var slipQuery = unitOfWork.BorrowSlips.GetQueryable(asNoTracking: true)
            .Where(s => s.Status != EntityStatus.Deleted && s.BorrowedAt >= fromOffset && s.BorrowedAt <= toOffset);
        var totalSlips = await slipQuery.CountAsync(ct);

        var detailsQuery = unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .Where(d => d.Status != EntityStatus.Deleted && d.BorrowSlip.Status != EntityStatus.Deleted
                        && d.BorrowSlip.BorrowedAt >= fromOffset && d.BorrowSlip.BorrowedAt <= toOffset);

        var totalBooksBorrowed = await detailsQuery.CountAsync(ct);
        var totalBooksReturned = await detailsQuery.CountAsync(d => d.ReturnedAt != null, ct);
        var totalBooksCurrentlyBorrowing = await detailsQuery.CountAsync(d => d.ReturnedAt == null, ct);
        var totalBooksOverdue = await detailsQuery.CountAsync(d => d.ReturnedAt == null && d.DueDate < today, ct);

        // 2. On-time return rate (client evaluation to safely handle DateOnly conversions)
        var returnedDetails = await detailsQuery
            .Where(d => d.ReturnedAt != null)
            .Select(d => new { d.DueDate, d.ReturnedAt })
            .ToListAsync(ct);

        var onTimeCount = returnedDetails.Count(d => DateOnly.FromDateTime(d.ReturnedAt!.Value.ToOffset(localOffset).DateTime) <= d.DueDate);
        var onTimeRate = returnedDetails.Count > 0
            ? Math.Round((double)onTimeCount * 100 / returnedDetails.Count, 1)
            : 100.0;

        // 3. Condition breakdown (fetch conditions as plain strings, group client-side)
        var returnConditions = await detailsQuery
            .Where(d => d.ReturnedAt != null && d.ReturnCondition != null)
            .Select(d => d.ReturnCondition!)
            .ToListAsync(ct);

        var conditionGood = returnConditions.Count(c => c == ReturnCondition.Good);
        var conditionDamaged = returnConditions.Count(c => c == ReturnCondition.Damaged);
        var conditionLost = returnConditions.Count(c => c == ReturnCondition.Lost);

        // 4. Fines in range
        var finesInRange = await unitOfWork.Fines.GetQueryable(asNoTracking: true)
            .Where(f => f.Status != EntityStatus.Deleted && f.FineStatus != FineStatus.Cancelled
                        && f.CreatedAt >= fromOffset && f.CreatedAt <= toOffset)
            .Select(f => new
            {
                f.FineType,
                f.FineStatus,
                f.Amount
            })
            .ToListAsync(ct);

        var totalFineIncurred = finesInRange.Sum(f => f.Amount);
        var totalFineCollected = finesInRange.Where(f => f.FineStatus == FineStatus.Paid).Sum(f => f.Amount);
        var totalFineWaived = finesInRange.Where(f => f.FineStatus == FineStatus.Waived).Sum(f => f.Amount);
        var totalFineUnpaid = finesInRange.Where(f => f.FineStatus == FineStatus.Unpaid).Sum(f => f.Amount);

        // 5. Daily trends (fetch raw DateTimeOffset and project DateOnly in memory)
        var rawTrendsDb = await detailsQuery
            .Select(d => new
            {
                d.BorrowSlip.BorrowedAt,
                d.DueDate,
                d.ReturnedAt
            })
            .ToListAsync(ct);

        var rawTrends = rawTrendsDb.Select(d => new
        {
            Date = DateOnly.FromDateTime(d.BorrowedAt.ToOffset(localOffset).DateTime),
            Overdue = d.ReturnedAt == null && d.DueDate < today
        }).ToList();

        var rawReturnsDb = await unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .Where(d => d.Status != EntityStatus.Deleted && d.BorrowSlip.Status != EntityStatus.Deleted && d.ReturnedAt != null
                        && d.ReturnedAt >= fromOffset && d.ReturnedAt <= toOffset)
            .Select(d => d.ReturnedAt)
            .ToListAsync(ct);

        var rawReturns = rawReturnsDb
            .Where(r => r.HasValue)
            .Select(r => DateOnly.FromDateTime(r!.Value.ToOffset(localOffset).DateTime))
            .ToList();

        var daysCount = Math.Min(31, rangeEnd.DayNumber - rangeStart.DayNumber + 1);
        var startTrendDate = rangeEnd.AddDays(-(daysCount - 1));
        var dailyList = new List<DailyCirculationItem>();
        for (var day = 0; day < daysCount; day++)
        {
            var date = startTrendDate.AddDays(day);
            var bCount = rawTrends.Count(t => t.Date == date);
            var rCount = rawReturns.Count(r => r == date);
            var oCount = rawTrends.Count(t => t.Date == date && t.Overdue);
            dailyList.Add(new DailyCirculationItem
            {
                Date = date,
                BorrowCount = bCount,
                ReturnCount = rCount,
                OverdueCount = oCount
            });
        }

        // 6. Overdue loans
        var overdueRaw = await unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .Where(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null && d.DueDate < today
                        && d.BorrowSlip.Status != EntityStatus.Deleted)
            .OrderBy(d => d.DueDate)
            .Take(100)
            .Select(d => new
            {
                DetailId = d.Id,
                d.SlipId,
                d.BorrowSlip.BorrowedAt,
                d.DueDate,
                ReaderName = d.BorrowSlip.Reader.FullName,
                d.BorrowSlip.Reader.CardNo,
                ReaderTypeName = d.BorrowSlip.Reader.ReaderType.Name,
                d.BorrowSlip.Reader.Phone,
                BookTitle = d.BookCopy.Book.Title,
                d.BookCopy.Barcode,
                ShelfCode = d.BookCopy.Shelf!.Code
            })
            .ToListAsync(ct);

        var overdueList = overdueRaw.Select(d =>
        {
            var days = Math.Max(0, today.DayNumber - d.DueDate.DayNumber);
            return new OverdueLoanItem
            {
                DetailId = d.DetailId,
                SlipId = d.SlipId,
                SlipCode = $"PM-{d.BorrowedAt:yyyyMMdd}-{d.SlipId.ToString()[..4].ToUpper()}",
                ReaderName = d.ReaderName,
                CardNo = d.CardNo,
                ReaderTypeName = d.ReaderTypeName,
                ReaderPhone = d.Phone,
                BookTitle = d.BookTitle,
                Barcode = d.Barcode,
                ShelfLocation = d.ShelfCode,
                BorrowedAt = d.BorrowedAt,
                DueDate = d.DueDate,
                OverdueDays = days,
                EstimatedFine = days * finePerDay
            };
        }).ToList();

        // 7. Top 10 borrowed books (group client-side to prevent complex GROUP BY SQL translation errors)
        var borrowedBookIds = await detailsQuery
            .Select(d => d.BookCopy.BookId)
            .ToListAsync(ct);

        var topBookIds = borrowedBookIds
            .GroupBy(id => id)
            .Select(g => new { BookId = g.Key, BorrowCount = g.Count() })
            .OrderByDescending(x => x.BorrowCount)
            .Take(10)
            .ToList();

        var bookIds = topBookIds.Select(x => x.BookId).ToList();
        var booksInfoRaw = await unitOfWork.Books.GetQueryable(asNoTracking: true)
            .Include(b => b.Category)
            .Include(b => b.BookCopies)
            .Include(b => b.BookAuthors.Where(ba => ba.Status != EntityStatus.Deleted))
                .ThenInclude(ba => ba.Author)
            .Where(b => bookIds.Contains(b.Id))
            .ToListAsync(ct);

        var topList = topBookIds.Select(t =>
        {
            var b = booksInfoRaw.FirstOrDefault(x => x.Id == t.BookId);
            var authors = b?.BookAuthors
                .Where(ba => ba.Status != EntityStatus.Deleted && ba.Author != null)
                .OrderBy(ba => ba.AuthorOrder)
                .Select(ba => ba.Author.FullName)
                .ToList() ?? [];

            return new TopBorrowedBookItem
            {
                BookId = t.BookId,
                Title = b?.Title ?? "—",
                Isbn = b?.Isbn ?? "—",
                Authors = authors.Count > 0 ? string.Join(", ", authors) : "—",
                Categories = b?.Category?.Name ?? "—",
                TotalCopies = b?.BookCopies.Count(c => c.Status != EntityStatus.Deleted) ?? 0,
                BorrowCount = t.BorrowCount
            };
        }).ToList();

        // 8. Fine summary by type
        var fineTypes = new[] { FineType.Overdue, FineType.Damaged, FineType.Lost };
        var fineSummary = fineTypes.Select(type =>
        {
            var matched = finesInRange.Where(f => f.FineType == type).ToList();
            var label = type switch
            {
                FineType.Overdue => "Quá hạn",
                FineType.Damaged => "Hỏng sách",
                FineType.Lost => "Mất sách",
                _ => type
            };
            return new FineTypeSummaryItem
            {
                FineType = type,
                TypeLabel = label,
                Count = matched.Count,
                TotalAmount = matched.Sum(x => x.Amount),
                PaidAmount = matched.Where(x => x.FineStatus == FineStatus.Paid).Sum(x => x.Amount),
                UnpaidAmount = matched.Where(x => x.FineStatus == FineStatus.Unpaid).Sum(x => x.Amount),
                WaivedAmount = matched.Where(x => x.FineStatus == FineStatus.Waived).Sum(x => x.Amount)
            };
        }).ToList();

        return new ReportDashboardViewModel
        {
            Filter = filter,
            RangeStart = rangeStart,
            RangeEnd = rangeEnd,
            Metrics = new ReportSummaryMetrics
            {
                TotalBorrowSlips = totalSlips,
                TotalBooksBorrowed = totalBooksBorrowed,
                TotalBooksReturned = totalBooksReturned,
                TotalBooksCurrentlyBorrowing = totalBooksCurrentlyBorrowing,
                TotalBooksOverdue = totalBooksOverdue,
                OnTimeReturnRate = onTimeRate,
                TotalFineIncurred = totalFineIncurred,
                TotalFineCollected = totalFineCollected,
                TotalFineWaived = totalFineWaived,
                TotalFineUnpaid = totalFineUnpaid
            },
            DailyTrends = dailyList,
            OverdueLoans = overdueList,
            TopBooks = topList,
            FineSummary = fineSummary,
            ReturnConditionGood = conditionGood,
            ReturnConditionDamaged = conditionDamaged,
            ReturnConditionLost = conditionLost
        };
    }

    public async Task<byte[]> ExportOverdueLoansCsvAsync(CancellationToken ct = default)
    {
        var today = dateTimeProvider.Today;
        var setting = await unitOfWork.LibrarySettings.GetQueryable(asNoTracking: true)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        var finePerDay = setting?.OverdueFinePerDay ?? 5000m;

        var overdueRaw = await unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .Where(d => d.Status != EntityStatus.Deleted && d.ReturnedAt == null && d.DueDate < today
                        && d.BorrowSlip.Status != EntityStatus.Deleted)
            .OrderBy(d => d.DueDate)
            .Select(d => new
            {
                d.SlipId,
                d.BorrowSlip.BorrowedAt,
                d.DueDate,
                ReaderName = d.BorrowSlip.Reader.FullName,
                d.BorrowSlip.Reader.CardNo,
                ReaderTypeName = d.BorrowSlip.Reader.ReaderType.Name,
                d.BorrowSlip.Reader.Phone,
                BookTitle = d.BookCopy.Book.Title,
                d.BookCopy.Barcode,
                ShelfCode = d.BookCopy.Shelf!.Code
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.Append('\uFEFF'); // UTF-8 BOM
        sb.AppendLine("STT,Mã phiếu,Số thẻ,Họ tên độc giả,Số điện thoại,Đối tượng,Mã vạch,Tên tài liệu,Kệ sách,Ngày mượn,Hạn trả,Số ngày trễ,Tiền phạt tạm tính");

        var stt = 1;
        foreach (var item in overdueRaw)
        {
            var slipCode = $"PM-{item.BorrowedAt:yyyyMMdd}-{item.SlipId.ToString()[..4].ToUpper()}";
            var overdueDays = Math.Max(0, today.DayNumber - item.DueDate.DayNumber);
            var estimatedFine = overdueDays * finePerDay;

            sb.AppendLine(string.Join(",",
                stt++,
                EscapeCsv(slipCode),
                EscapeCsv(item.CardNo),
                EscapeCsv(item.ReaderName),
                EscapeCsv(item.Phone ?? ""),
                EscapeCsv(item.ReaderTypeName),
                EscapeCsv(item.Barcode),
                EscapeCsv(item.BookTitle),
                EscapeCsv(item.ShelfCode ?? ""),
                item.BorrowedAt.ToOffset(dateTimeProvider.Now.Offset).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                item.DueDate.ToString(AppConstants.DefaultDateFormat),
                overdueDays,
                estimatedFine.ToString(CultureInfo.InvariantCulture)
            ));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static (DateOnly Start, DateOnly End) ResolveDateRange(ReportFilter filter, DateOnly today)
    {
        if (filter.Preset == "today")
            return (today, today);

        if (filter.Preset == "week")
            return (today.AddDays(-6), today);

        if (filter.Preset == "month")
            return (new DateOnly(today.Year, today.Month, 1), today);

        if (filter.Preset == "year")
            return (new DateOnly(today.Year, 1, 1), today);

        if (filter.FromDate.HasValue && filter.ToDate.HasValue)
        {
            return filter.FromDate.Value <= filter.ToDate.Value
                ? (filter.FromDate.Value, filter.ToDate.Value)
                : (filter.ToDate.Value, filter.FromDate.Value);
        }

        if (filter.FromDate.HasValue)
            return filter.FromDate.Value <= today ? (filter.FromDate.Value, today) : (today, filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            return (DateOnly.FromDayNumber(Math.Max(0, filter.ToDate.Value.DayNumber - 30)), filter.ToDate.Value);

        return (DateOnly.FromDayNumber(Math.Max(0, today.DayNumber - 30)), today);
    }

    private static string EscapeCsv(string field)
    {
        // CSV quoting alone does not stop spreadsheet formula interpretation.
        var trimmed = field.AsSpan().TrimStart();
        if ((trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@') ||
            (field.Length > 0 && field[0] is '\t' or '\r' or '\n'))
        {
            field = "'" + field;
        }
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
