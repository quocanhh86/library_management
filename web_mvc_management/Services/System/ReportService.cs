using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using web_mvc_management.Constant;
using web_mvc_management.Helper;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Reports;
using web_mvc_management.Repositories;
using static web_mvc_management.Constant.LibraryConstants;

namespace web_mvc_management.Services;

public class ReportService(
    IUnitOfWork unitOfWork,
    ICirculationPolicy circulationPolicy,
    IDateTimeProvider dateTimeProvider) : IReportService
{
    public async Task<ReportDashboardViewModel> GetReportDashboardAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var today = dateTimeProvider.Today;
        var (rangeStart, rangeEnd) = ResolveDateRange(filter, today);

        var localOffset = dateTimeProvider.Now.Offset;
        var fromOffset = DateTimeHelper.UtcDayBoundary(rangeStart, localOffset);
        var toOffset = DateTimeHelper.UtcDayBoundary(rangeEnd, localOffset, endOfDay: true);

        var setting = await circulationPolicy.GetEffectiveSettingAsync(ct);
        var finePerDay = setting.OverdueFinePerDay;

        var totalSlips = await unitOfWork.BorrowSlips.GetQueryable(asNoTracking: true)
            .CountAsync(s => s.Status != EntityStatus.Deleted && s.BorrowedAt >= fromOffset && s.BorrowedAt <= toOffset, ct);

        var detailsQuery = unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .Where(d => d.Status != EntityStatus.Deleted
                        && d.BorrowSlip.Status != EntityStatus.Deleted
                        && d.BorrowSlip.BorrowedAt >= fromOffset
                        && d.BorrowSlip.BorrowedAt <= toOffset);

        var totalBooksBorrowed = await detailsQuery.CountAsync(ct);
        var totalBooksReturned = await detailsQuery.CountAsync(d => d.ReturnedAt != null, ct);
        var totalBooksCurrentlyBorrowing = await detailsQuery.CountAsync(d => d.ReturnedAt == null, ct);
        var totalBooksOverdue = await detailsQuery.CountAsync(d => d.ReturnedAt == null && d.DueDate < today, ct);

        var returnedDetails = await detailsQuery
            .Where(d => d.ReturnedAt != null)
            .Select(d => new { d.DueDate, d.ReturnedAt })
            .ToListAsync(ct);

        var onTimeCount = returnedDetails.Count(d =>
            DateOnly.FromDateTime(d.ReturnedAt!.Value.ToOffset(localOffset).DateTime) <= d.DueDate);
        var onTimeRate = returnedDetails.Count > 0
            ? Math.Round((double)onTimeCount * 100 / returnedDetails.Count, 1)
            : 100.0;

        var returnConditions = await detailsQuery
            .Where(d => d.ReturnedAt != null && d.ReturnCondition != null)
            .Select(d => d.ReturnCondition!)
            .ToListAsync(ct);

        var finesInRange = await unitOfWork.Fines.GetQueryable(asNoTracking: true)
            .Where(f => f.Status != EntityStatus.Deleted
                        && f.FineStatus != FineStatus.Cancelled
                        && f.CreatedAt >= fromOffset
                        && f.CreatedAt <= toOffset)
            .Select(f => new { f.FineType, f.FineStatus, f.Amount })
            .ToListAsync(ct);

        var rawTrendsDb = await detailsQuery
            .Select(d => new { d.BorrowSlip.BorrowedAt, d.DueDate, d.ReturnedAt })
            .ToListAsync(ct);

        var rawTrends = rawTrendsDb.Select(d => new
        {
            Date = DateOnly.FromDateTime(d.BorrowedAt.ToOffset(localOffset).DateTime),
            Overdue = d.ReturnedAt == null && d.DueDate < today
        }).ToList();

        var rawReturnsDb = await unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .Where(d => d.Status != EntityStatus.Deleted
                        && d.BorrowSlip.Status != EntityStatus.Deleted
                        && d.ReturnedAt != null
                        && d.ReturnedAt >= fromOffset
                        && d.ReturnedAt <= toOffset)
            .Select(d => d.ReturnedAt)
            .ToListAsync(ct);

        var rawReturns = rawReturnsDb
            .Where(r => r.HasValue)
            .Select(r => DateOnly.FromDateTime(r!.Value.ToOffset(localOffset).DateTime))
            .ToList();

        var daysCount = Math.Min(31, rangeEnd.DayNumber - rangeStart.DayNumber + 1);
        var startTrendDate = rangeEnd.AddDays(-(daysCount - 1));
        var dailyList = Enumerable.Range(0, daysCount)
            .Select(day =>
            {
                var date = startTrendDate.AddDays(day);
                return new DailyCirculationItem
                {
                    Date = date,
                    BorrowCount = rawTrends.Count(t => t.Date == date),
                    ReturnCount = rawReturns.Count(r => r == date),
                    OverdueCount = rawTrends.Count(t => t.Date == date && t.Overdue)
                };
            })
            .ToList();

        var overdueRaw = await OverdueQuery(today).Take(100).ToListAsync(ct);
        var overdueList = overdueRaw.Select(d =>
        {
            var days = Math.Max(0, today.DayNumber - d.DueDate.DayNumber);
            return new OverdueLoanItem
            {
                DetailId = d.DetailId,
                SlipId = d.SlipId,
                SlipCode = SlipCodeHelper.Format(d.SlipId, d.BorrowedAt),
                ReaderName = d.ReaderName,
                CardNo = d.CardNo,
                ReaderTypeName = d.ReaderTypeName,
                ReaderPhone = d.Phone,
                BookTitle = d.BookTitle,
                Barcode = d.Barcode,
                ShelfLocation = d.ShelfCode ?? "—",
                BorrowedAt = d.BorrowedAt,
                DueDate = d.DueDate,
                OverdueDays = days,
                EstimatedFine = days * finePerDay
            };
        }).ToList();

        var topBookIds = (await detailsQuery.Select(d => d.BookCopy.BookId).ToListAsync(ct))
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
            .AsSplitQuery()
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

        string[] fineTypes = [FineType.Overdue, FineType.Damaged, FineType.Lost];
        var fineSummary = fineTypes.Select(type =>
        {
            var matched = finesInRange.Where(f => f.FineType == type).ToList();
            return new FineTypeSummaryItem
            {
                FineType = type,
                TypeLabel = type switch
                {
                    FineType.Overdue => "Quá hạn",
                    FineType.Damaged => "Hỏng sách",
                    FineType.Lost => "Mất sách",
                    _ => type
                },
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
                TotalFineIncurred = finesInRange.Sum(f => f.Amount),
                TotalFineCollected = finesInRange.Where(f => f.FineStatus == FineStatus.Paid).Sum(f => f.Amount),
                TotalFineWaived = finesInRange.Where(f => f.FineStatus == FineStatus.Waived).Sum(f => f.Amount),
                TotalFineUnpaid = finesInRange.Where(f => f.FineStatus == FineStatus.Unpaid).Sum(f => f.Amount)
            },
            DailyTrends = dailyList,
            OverdueLoans = overdueList,
            TopBooks = topList,
            FineSummary = fineSummary,
            ReturnConditionGood = returnConditions.Count(c => c == ReturnCondition.Good),
            ReturnConditionDamaged = returnConditions.Count(c => c == ReturnCondition.Damaged),
            ReturnConditionLost = returnConditions.Count(c => c == ReturnCondition.Lost)
        };
    }

    public async Task<byte[]> ExportOverdueLoansCsvAsync(CancellationToken ct = default)
    {
        var today = dateTimeProvider.Today;
        var finePerDay = (await circulationPolicy.GetEffectiveSettingAsync(ct)).OverdueFinePerDay;
        var overdueRaw = await OverdueQuery(today).ToListAsync(ct);

        var sb = new StringBuilder();
        sb.Append('\uFEFF'); // UTF-8 BOM
        sb.AppendLine("STT,Mã phiếu,Số thẻ,Họ tên độc giả,Số điện thoại,Đối tượng,Mã vạch,Tên tài liệu,Kệ sách,Ngày mượn,Hạn trả,Số ngày trễ,Tiền phạt tạm tính");

        var stt = 1;
        var offset = dateTimeProvider.Now.Offset;
        foreach (var item in overdueRaw)
        {
            var slipCode = SlipCodeHelper.Format(item.SlipId, item.BorrowedAt);
            var overdueDays = Math.Max(0, today.DayNumber - item.DueDate.DayNumber);
            var estimatedFine = overdueDays * finePerDay;

            sb.AppendLine(string.Join(",",
                stt++,
                EscapeCsv(slipCode),
                EscapeCsv(item.CardNo),
                EscapeCsv(item.ReaderName),
                EscapeCsv(item.Phone ?? string.Empty),
                EscapeCsv(item.ReaderTypeName),
                EscapeCsv(item.Barcode),
                EscapeCsv(item.BookTitle),
                EscapeCsv(item.ShelfCode ?? string.Empty),
                item.BorrowedAt.ToOffset(offset).ToString(AppConstants.DefaultDateFormat, CultureInfo.InvariantCulture),
                item.DueDate.ToString(AppConstants.DefaultDateFormat),
                overdueDays,
                estimatedFine.ToString(CultureInfo.InvariantCulture)));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private sealed record OverdueProjection(
        Guid DetailId,
        Guid SlipId,
        DateTimeOffset BorrowedAt,
        DateOnly DueDate,
        string ReaderName,
        string CardNo,
        string ReaderTypeName,
        string? Phone,
        string BookTitle,
        string Barcode,
        string? ShelfCode);

    private IQueryable<OverdueProjection> OverdueQuery(DateOnly today) =>
        unitOfWork.BorrowDetails.GetQueryable(asNoTracking: true)
            .Where(d => d.Status != EntityStatus.Deleted
                        && d.ReturnedAt == null
                        && d.DueDate < today
                        && d.BorrowSlip.Status != EntityStatus.Deleted)
            .OrderBy(d => d.DueDate)
            .Select(d => new OverdueProjection(
                d.Id,
                d.SlipId,
                d.BorrowSlip.BorrowedAt,
                d.DueDate,
                d.BorrowSlip.Reader.FullName,
                d.BorrowSlip.Reader.CardNo,
                d.BorrowSlip.Reader.ReaderType.Name,
                d.BorrowSlip.Reader.Phone,
                d.BookCopy.Book.Title,
                d.BookCopy.Barcode,
                d.BookCopy.Shelf != null ? d.BookCopy.Shelf.Code : null));

    private static (DateOnly Start, DateOnly End) ResolveDateRange(ReportFilter filter, DateOnly today) =>
        filter.Preset switch
        {
            "today" => (today, today),
            "week" => (today.AddDays(-6), today),
            "month" => (new DateOnly(today.Year, today.Month, 1), today),
            "year" => (new DateOnly(today.Year, 1, 1), today),
            _ when filter.FromDate.HasValue && filter.ToDate.HasValue =>
                filter.FromDate.Value <= filter.ToDate.Value
                    ? (filter.FromDate.Value, filter.ToDate.Value)
                    : (filter.ToDate.Value, filter.FromDate.Value),
            _ when filter.FromDate.HasValue =>
                filter.FromDate.Value <= today ? (filter.FromDate.Value, today) : (today, filter.FromDate.Value),
            _ when filter.ToDate.HasValue =>
                (DateOnly.FromDayNumber(Math.Max(0, filter.ToDate.Value.DayNumber - 30)), filter.ToDate.Value),
            _ => (DateOnly.FromDayNumber(Math.Max(0, today.DayNumber - 30)), today)
        };

    private static string EscapeCsv(string field)
    {
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
