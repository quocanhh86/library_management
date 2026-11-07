using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Extensions;
using web_mvc_management.Models.ViewModels.AdminCatalog;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Services;

namespace LibraryManagement.Tests;

public class ServiceTests : IAsyncLifetime
{
    private readonly TestDatabase f = new();
    public Task InitializeAsync() => f.InitializeAsync();
    public Task DisposeAsync() => f.DisposeAsync().AsTask();

    [Fact] public async Task OtpIsSingleUseIsolatedByPurposeAndExpires()
    {
        var otp = new OtpService(f.Uow, f.Clock);
        var code = await otp.GenerateOtpAsync(" TEST@example.invalid ", "LOGIN");
        Assert.Equal(6, code.Length);
        Assert.False((await otp.VerifyOtpAsync("test@example.invalid", "REGISTER", code)).IsSuccess);
        Assert.True((await otp.VerifyOtpAsync("test@example.invalid", "LOGIN", code)).IsSuccess);
        Assert.False((await otp.VerifyOtpAsync("test@example.invalid", "LOGIN", code)).IsSuccess);
        code = await otp.GenerateOtpAsync("test@example.invalid", "LOGIN");
        f.Clock.UtcNow = f.Clock.UtcNow.AddMinutes(5);
        Assert.Equal(OtpStatus.Expired, (await otp.VerifyOtpAsync("test@example.invalid", "LOGIN", code)).Status);
    }
    [Fact] public async Task OtpBurnsAfterFiveWrongAttemptsAndResendInvalidatesOldCode()
    {
        var otp = new OtpService(f.Uow, f.Clock);
        var code = await otp.GenerateOtpAsync("test@example.invalid", "LOGIN");
        for (int i = 0; i < 4; i++) Assert.Equal(OtpStatus.Incorrect, (await otp.VerifyOtpAsync("test@example.invalid", "LOGIN", "000000")).Status);
        Assert.Equal(OtpStatus.Burned, (await otp.VerifyOtpAsync("test@example.invalid", "LOGIN", "000000")).Status);
        Assert.False((await otp.VerifyOtpAsync("test@example.invalid", "LOGIN", code)).IsSuccess);
        await otp.GenerateOtpAsync("test@example.invalid", "LOGIN");
        await otp.GenerateOtpAsync("test@example.invalid", "LOGIN");
        Assert.Equal(1, await f.Db.Otps.CountAsync(x => !x.IsUsed));
        await otp.InvalidateOtpAsync("test@example.invalid", "LOGIN");
        Assert.Equal(0, await f.Db.Otps.CountAsync(x => !x.IsUsed));
    }
    [Fact] public async Task MasterDataCrudAndDuplicateProtection()
    {
        var service = new CatalogMasterService(f.Uow);
        Assert.True((await service.SaveCategoryAsync(new() { Name = "Category" })).Succeeded);
        Assert.False((await service.SaveCategoryAsync(new() { Name = "Category" })).Succeeded);
        var id = await f.Db.Categories.Select(x => x.Id).SingleAsync();
        Assert.True((await service.SaveCategoryAsync(new() { Id = id, Name = "Updated" })).Succeeded);
        Assert.True((await service.DeleteCategoryAsync(id)).Succeeded);
        Assert.Empty(await service.GetActiveCategoriesAsync());
        Assert.True((await service.RestoreCategoryAsync(id)).Succeeded);
        Assert.False((await service.HardDeleteCategoryAsync(id, "wrong")).Succeeded);
        Assert.True((await service.DeleteCategoryAsync(id)).Succeeded);
        Assert.True((await service.HardDeleteCategoryAsync(id, "XOA")).Succeeded);
        Assert.True((await service.SaveAuthorAsync(new() { FullName = "Author" })).Succeeded);
        Assert.True((await service.SavePublisherAsync(new() { Name = "Publisher" })).Succeeded);
        Assert.True((await service.SaveShelfAsync(new() { Code = "A1" })).Succeeded);
        Assert.Single(await service.GetActiveAuthorsAsync());
        Assert.Single(await service.GetActivePublishersAsync());
        Assert.Single(await service.GetActiveShelvesAsync());
    }
    [Fact] public async Task StaffCreationDuplicateSelfDeletionAndSettings()
    {
        var service = new SystemAdminService(f.Uow, f.User);
        var input = new StaffCreateViewModel { Username = "newstaff", Email = "new@example.invalid", FullName = "New staff", Password = "Test-only!123" };
        Assert.True((await service.CreateStaffAsync(input)).Succeeded);
        Assert.False((await service.CreateStaffAsync(input)).Succeeded);
        Assert.False((await service.SoftDeleteStaffAsync(f.User.CurrentUserId!.Value)).Succeeded);
        var id = await f.Db.Staff.Where(x => x.Username == input.Username).Select(x => x.Id).SingleAsync();
        Assert.True((await service.SoftDeleteStaffAsync(id)).Succeeded);
        Assert.True((await service.RestoreStaffAsync(id)).Succeeded);
        var settings = await service.GetLibrarySettingAsync();
        settings.OverdueFinePerDay = 3000;
        Assert.True((await service.SaveLibrarySettingAsync(settings)).Succeeded);
        Assert.Equal(3000, (await service.GetLibrarySettingAsync()).OverdueFinePerDay);
        Assert.NotEmpty(await service.GetReaderTypesAsync());
    }
    [Fact] public async Task BooksCreateEditCopiesDeleteRestoreAndPublicSearch()
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Category" };
        var publisher = new Publisher { Id = Guid.NewGuid(), Name = "Publisher" };
        var author = new Author { Id = Guid.NewGuid(), FullName = "Author" };
        var shelf = new Shelf { Id = Guid.NewGuid(), Code = "A1" };
        f.Db.AddRange(category, publisher, author, shelf);
        await f.Db.SaveChangesAsync();
        var service = new BookService(f.Uow, f.Clock, new NoImages());
        var input = new BookUpsertViewModel { Title = "Unique title", CategoryId = category.Id, PublisherId = publisher.Id, AuthorIds = [author.Id], InitialCopyCount = 2, ShelfId = shelf.Id };
        var created = await service.CreateBookWithCopiesAsync(input);
        Assert.True(created.Succeeded, created.Message);
        Assert.Equal(2, await f.Db.BookCopies.CountAsync(x => x.BookId == created.Data));
        Assert.Single(await service.SuggestAsync("Unique"));
        input.Id = created.Data;
        input.Title = "Updated title";
        Assert.True((await service.UpdateBookAsync(input)).Succeeded);
        Assert.NotNull(await service.GetPublicBookDetailAsync(created.Data));
        Assert.True((await service.DeleteBookAsync(created.Data)).Succeeded);
        Assert.Null(await service.GetPublicBookDetailAsync(created.Data));
        Assert.True((await service.RestoreBookAsync(created.Data)).Succeeded);
        Assert.NotNull(await service.GetPublicBookDetailAsync(created.Data));
    }
    [Fact] public async Task HugePageNumberDoesNotOverflowOrReturnFirstPage()
    {
        var paged = await f.Db.Books.OrderBy(x => x.Id).ToPagedResultAsync(int.MaxValue, 10);
        Assert.Empty(paged.Items);
        Assert.Empty(Enumerable.Range(1, 20).ToPagedResult(int.MaxValue, 10).Items);
        await f.Circulation.GetBorrowHistoryPageAsync(new() { PageNo = int.MaxValue });
    }
    [Theory]
    [InlineData("0001-01-01")]
    [InlineData("9999-12-31")]
    public async Task ReportHandlesDateBoundaries(string date)
    {
        var report = new ReportService(f.Uow, f.Clock);
        var result = await report.GetReportDashboardAsync(new() { Preset = "custom", ToDate = DateOnly.Parse(date) });
        Assert.NotEmpty(result.DailyTrends);
    }
    [Fact] public async Task ReportAndCsvWorkWithOverdueBookWithoutShelf()
    {
        Assert.True((await f.Circulation.CreateBorrowSlipAsync(new() { ReaderId = f.Reader.Id, Barcodes = [f.Copy.Barcode] })).Succeeded);
        f.Clock.UtcNow = f.Clock.UtcNow.AddDays(17);
        var report = new ReportService(f.Uow, f.Clock);
        var result = await report.GetReportDashboardAsync(new() { Preset = "year" });
        Assert.Equal(1, result.Metrics.TotalBooksBorrowed);
        Assert.Single(result.OverdueLoans);
        Assert.Equal(6000, result.OverdueLoans[0].EstimatedFine);
        var csv = Encoding.UTF8.GetString(await report.ExportOverdueLoansCsvAsync());
        Assert.StartsWith("\uFEFF", csv);
        Assert.Contains("TEST-COPY", csv);
    }
    private sealed class NoImages : ICloudinaryService
    {
        public Task<ServiceResult<ImageUploadResult>> UploadBookImageAsync(IFormFile file, CancellationToken ct = default) => throw new InvalidOperationException("Unexpected external upload");
        public Task<ServiceResult> DeleteImageAsync(string? publicId, CancellationToken ct = default) => Task.FromResult(ServiceResult.Success());
    }
    [Fact] public async Task ReportsAndHistoryIncludeLocalMidnightLoans()
    {
        f.Clock.UtcNow = new DateTimeOffset(2026, 11, 6, 17, 30, 0, TimeSpan.Zero);
        Assert.True((await f.Circulation.CreateBorrowSlipAsync(new() { ReaderId = f.Reader.Id, Barcodes = [f.Copy.Barcode] })).Succeeded);
        var report = await new ReportService(f.Uow, f.Clock).GetReportDashboardAsync(new() { Preset = "today" });
        Assert.Equal(1, report.Metrics.TotalBooksBorrowed);
        Assert.Equal(1, report.DailyTrends.Single().BorrowCount);
        var history = await f.Circulation.GetBorrowHistoryPageAsync(new() { FromDate = f.Clock.Today, ToDate = f.Clock.Today });
        Assert.Equal(1, history.Slips.TotalCount);
    }
}
