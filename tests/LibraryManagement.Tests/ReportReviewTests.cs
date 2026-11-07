using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Services;
using static web_mvc_management.Constant.LibraryConstants;

namespace LibraryManagement.Tests;

public class ReportReviewTests : IAsyncLifetime
{
    private readonly TestDatabase f = new();
    public Task InitializeAsync() => f.InitializeAsync();
    public Task DisposeAsync() => f.DisposeAsync().AsTask();
    private ReportService Report => new(f.Uow, f.Clock);
    private async Task<BorrowDetail> Loan()
    {
        var result = await f.Circulation.CreateBorrowSlipAsync(new() { ReaderId = f.Reader.Id, Barcodes = [f.Copy.Barcode] });
        Assert.True(result.Succeeded, result.Message);
        return await f.Db.BorrowDetails.Include(d => d.BorrowSlip).SingleAsync();
    }
    [Theory]
    [InlineData("vi-VN")]
    [InlineData("en-US")]
    public async Task CsvHasThirteenColumnsAndInvariantAmount(string culture)
    {
        await Loan();
        f.Copy.Book.Title = "Sách, có \"dấu ngoặc\"\nvà xuống dòng";
        await f.Db.SaveChangesAsync();
        f.Clock.UtcNow = f.Clock.UtcNow.AddDays(17);
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var row = ReadDataRow(await Report.ExportOverdueLoansCsvAsync());
            Assert.Equal(13, row.Length);
            Assert.Equal(f.Copy.Book.Title, row[7]);
            Assert.Equal(6000m, decimal.Parse(row[12], CultureInfo.InvariantCulture));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+123")]
    [InlineData("-123")]
    [InlineData("@SUM(1)")]
    [InlineData("\t=1+1")]
    public async Task CsvTreatsUserTextAsTextRatherThanFormula(string title)
    {
        await Loan();
        f.Copy.Book.Title = title;
        await f.Db.SaveChangesAsync();
        f.Clock.UtcNow = f.Clock.UtcNow.AddDays(17);
        var row = ReadDataRow(await Report.ExportOverdueLoansCsvAsync());
        Assert.Equal("'" + title, row[7]);
    }
    [Fact] public async Task DeletedSlipDoesNotContributeToReturnTrend()
    {
        var loan = await Loan();
        loan.ReturnedAt = f.Clock.UtcNow;
        loan.ReturnCondition = ReturnCondition.Good;
        loan.BorrowSlip.Status = EntityStatus.Deleted;
        await f.Db.SaveChangesAsync();
        var report = await Report.GetReportDashboardAsync(new() { Preset = "today" });
        Assert.Equal(0, report.Metrics.TotalBooksBorrowed);
        Assert.Equal(0, report.DailyTrends.Sum(d => d.ReturnCount));
        Assert.Empty(report.TopBooks);
    }
    [Fact] public async Task CancelledFineDoesNotIncreaseIncurredTotal()
    {
        var loan = await Loan();
        f.Db.Fines.Add(new Fine { Id = Guid.NewGuid(), BorrowDetailId = loan.Id, Amount = 10000, FineStatus = FineStatus.Cancelled });
        await f.Db.SaveChangesAsync();
        var report = await Report.GetReportDashboardAsync(new() { Preset = "today" });
        Assert.Equal(0, report.Metrics.TotalFineIncurred);
        Assert.Equal(0, report.FineSummary.Sum(x => x.Count));
    }
    [Fact] public async Task ReturnedConditionsTopBooksAndRatesMatchKnownData()
    {
        var loan = await Loan();
        loan.ReturnedAt = f.Clock.UtcNow;
        loan.ReturnCondition = ReturnCondition.Damaged;
        await f.Db.SaveChangesAsync();
        var report = await Report.GetReportDashboardAsync(new() { Preset = "today" });
        Assert.Equal(1, report.Metrics.TotalBooksReturned);
        Assert.Equal(100, report.Metrics.OnTimeReturnRate);
        Assert.Equal(1, report.ReturnConditionDamaged);
        Assert.Equal(0, report.ReturnConditionGood);
        Assert.Equal(f.Copy.BookId, Assert.Single(report.TopBooks).BookId);
        Assert.Equal(1, report.TopBooks[0].BorrowCount);
        Assert.Equal(1, report.DailyTrends.Single().ReturnCount);
    }
    private static string[] ReadDataRow(byte[] bytes)
    {
        using var parser = new TextFieldParser(new StringReader(Encoding.UTF8.GetString(bytes))) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(",");
        parser.ReadFields();
        return parser.ReadFields()!;
    }
    [Fact] public async Task HistoryReturnedFilterIgnoresDeletedDetails()
    {
        var loan = await Loan();
        loan.ReturnedAt = f.Clock.UtcNow;
        f.Db.BorrowDetails.Add(new BorrowDetail { Id = Guid.NewGuid(), SlipId = loan.SlipId, CopyId = loan.CopyId, DueDate = loan.DueDate, Status = EntityStatus.Deleted });
        await f.Db.SaveChangesAsync();
        var history = await f.Circulation.GetBorrowHistoryPageAsync(new() { BorrowStatus = "returned" });
        Assert.Equal(1, history.TotalReturnedSlips);
        Assert.True(Assert.Single(history.Slips.Items).IsFullyReturned);
    }
    [Fact] public async Task HistoryDoesNotCountEmptySlipAsFullyReturned()
    {
        f.Db.BorrowSlips.Add(new BorrowSlip { Id = Guid.NewGuid(), ReaderId = f.Reader.Id, IssuedBy = f.User.CurrentUserId!.Value, BorrowedAt = f.Clock.UtcNow });
        await f.Db.SaveChangesAsync();
        var history = await f.Circulation.GetBorrowHistoryPageAsync(new() { BorrowStatus = "returned" });
        Assert.Equal(0, history.TotalReturnedSlips);
        Assert.Empty(history.Slips.Items);
    }
    [Theory]
    [InlineData(24, true)]
    [InlineData(25, false)]
    public async Task CancellationHonorsTwentyFourHourWindow(int hours, bool allowed)
    {
        var loan = await Loan();
        f.Clock.UtcNow = f.Clock.UtcNow.AddHours(hours);
        Assert.Equal(allowed, (await f.Circulation.GetBorrowSlipDetailAsync(loan.SlipId))!.CanCancel);
        Assert.Equal(allowed, (await f.Circulation.CancelBorrowSlipAsync(loan.SlipId, "Test correction")).Succeeded);
    }
    [Fact] public async Task ReturnedSlipCannotBeCancelled()
    {
        var loan = await Loan();
        Assert.True((await f.Circulation.ReturnAsync(new() { ReaderId = f.Reader.Id, Items = [new() { DetailId = loan.Id, Selected = true }] })).Succeeded);
        Assert.False((await f.Circulation.GetBorrowSlipDetailAsync(loan.SlipId))!.CanCancel);
        Assert.False((await f.Circulation.CancelBorrowSlipAsync(loan.SlipId, "Test correction")).Succeeded);
    }
}
