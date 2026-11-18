using Microsoft.EntityFrameworkCore;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Models.ViewModels.Reader;
using web_mvc_management.Models.ViewModels.Reports;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Tests.TestInfrastructure;
using Xunit;
using static web_mvc_management.Constant.LibraryConstants;

namespace web_mvc_management.Tests;

public class ReaderPortalAndAdminTests
{
    [Fact]
    public async Task ReaderPortalGets_ShouldBePureCqs_NeverMutateDatabaseForCleanReader()
    {
        await using var fx = new SqliteTestDbFixture();
        await fx.SeedSettingAsync();
        var rt = await fx.SeedReaderTypeAsync();
        var reader = await fx.SeedReaderAsync(rt.Id, "SV301");

        var portal = fx.CreateReaderPortalService();

        var homeLoans = await portal.GetHomeLoansAsync(reader.Id);
        var profile = await portal.GetProfilePageAsync(reader.Id);
        var history = await portal.GetBorrowHistoryPageAsync(reader.Id, new ReaderBorrowHistoryFilter());
        var fines = await portal.GetFinesPageAsync(reader.Id, new ReaderFineFilter());
        var reservations = await portal.GetReservationsPageAsync(reader.Id);

        Assert.NotNull(homeLoans);
        Assert.NotNull(profile);
        Assert.NotNull(history);
        Assert.NotNull(fines);
        Assert.NotNull(reservations);

        // Không được tự sinh bất kỳ BorrowSlip / Fine / Reservation demo nào
        Assert.Empty(homeLoans.Loans);
        Assert.Equal(0, profile.ActiveLoanCount);
        Assert.Equal(0, history.TotalHistoryCount);
        Assert.Empty(fines.Fines);
        Assert.Empty(reservations.Reservations);
        Assert.Equal(0, await fx.Context.BorrowSlips.CountAsync());
        Assert.Equal(0, await fx.Context.Reservations.CountAsync());
        Assert.Equal(0, await fx.Context.Fines.CountAsync());
    }

    [Fact]
    public async Task ReaderPortal_RenewAndReservationLifecycle_ShouldPromoteNextPendingOnCancel()
    {
        await using var fx = new SqliteTestDbFixture();
        await fx.SeedSettingAsync(holdHours: 48);
        var staff = await fx.SeedStaffAsync();
        var rt = await fx.SeedReaderTypeAsync(maxRenewals: 1, renewDays: 7);

        var borrower = await fx.SeedReaderAsync(rt.Id, "SV401");
        var firstWaiter = await fx.SeedReaderAsync(rt.Id, "SV402");
        var secondWaiter = await fx.SeedReaderAsync(rt.Id, "SV403");

        var (book, copies, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Kinh tế vận tải", copyCount: 1);
        var (_, detail) = await fx.SeedOpenLoanAsync(borrower.Id, staff.Id, copies[0].Id, dueDate: fx.DateTime.Today.AddDays(5));

        var portal = fx.CreateReaderPortalService();

        // Gia hạn khi chưa có ai đặt trước -> thành công; lần thứ 2 vượt maxRenewals = 1 -> thất bại
        var renew1 = await portal.RenewLoanAsync(borrower.Id, detail.Id);
        Assert.True(renew1.IsSuccess, renew1.Message);
        var renew2 = await portal.RenewLoanAsync(borrower.Id, detail.Id);
        Assert.False(renew2.IsSuccess);
        Assert.Equal(ErrorCode.RenewalLimitReached, renew2.Code);

        // SV402 và SV403 cùng đặt trước
        Assert.True((await portal.CreateReservationAsync(firstWaiter.Id, book.Id)).IsSuccess);
        fx.DateTime.AdvanceHours(1);
        Assert.True((await portal.CreateReservationAsync(secondWaiter.Id, book.Id)).IsSuccess);

        // Mô phỏng bản sao đã được giữ READY cho SV402
        var firstRes = await fx.Context.Reservations.FirstAsync(r => r.ReaderId == firstWaiter.Id);
        firstRes.ReservationStatus = ReservationStatus.Ready;
        firstRes.AssignedCopyId = copies[0].Id;
        firstRes.ReadyAt = fx.DateTime.UtcNow;
        firstRes.ExpiresAt = fx.DateTime.UtcNow.AddHours(48);
        var trackedCopy = await fx.Context.BookCopies.FirstAsync(c => c.Id == copies[0].Id);
        trackedCopy.CopyStatus = CopyStatus.Reserved;
        await fx.Context.SaveChangesAsync();

        // SV402 hủy đặt -> tự động nhường cho SV403
        var cancelRes = await portal.CancelReservationAsync(firstWaiter.Id, firstRes.Id);
        Assert.True(cancelRes.IsSuccess, cancelRes.Message);

        var nextRes = await fx.Context.Reservations.AsNoTracking().FirstAsync(r => r.ReaderId == secondWaiter.Id);
        var copyAfter = await fx.Context.BookCopies.AsNoTracking().FirstAsync(c => c.Id == copies[0].Id);
        Assert.Equal(ReservationStatus.Ready, nextRes.ReservationStatus);
        Assert.Equal(copies[0].Id, nextRes.AssignedCopyId);
        Assert.Equal(CopyStatus.Reserved, copyAfter.CopyStatus);
    }

    [Fact]
    public async Task ReaderAdmin_LockOrSoftDelete_ShouldRotateSecurityStamp_AndReleaseHeldCopyToNextWaiter()
    {
        await using var fx = new SqliteTestDbFixture();
        await fx.SeedSettingAsync();
        var rt = await fx.SeedReaderTypeAsync();

        var lockedTarget = await fx.SeedReaderAsync(rt.Id, "SV601");
        var nextWaiter = await fx.SeedReaderAsync(rt.Id, "SV602");
        var originalStamp = lockedTarget.SecurityStamp;

        var (book, copies, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Giải tích 2", copyCount: 1);
        copies[0].CopyStatus = CopyStatus.Reserved;

        var readyRes = new Reservation
        {
            Id = Guid.NewGuid(),
            BookId = book.Id,
            ReaderId = lockedTarget.Id,
            AssignedCopyId = copies[0].Id,
            ReservationStatus = ReservationStatus.Ready,
            ReservedAt = fx.DateTime.UtcNow.AddDays(-2),
            ReadyAt = fx.DateTime.UtcNow.AddHours(-2),
            ExpiresAt = fx.DateTime.UtcNow.AddHours(46),
            Status = EntityStatus.Active
        };
        var pendingRes = new Reservation
        {
            Id = Guid.NewGuid(),
            BookId = book.Id,
            ReaderId = nextWaiter.Id,
            ReservationStatus = ReservationStatus.Pending,
            ReservedAt = fx.DateTime.UtcNow.AddDays(-1),
            Status = EntityStatus.Active
        };
        await fx.Context.Reservations.AddRangeAsync(readyRes, pendingRes);
        await fx.Context.SaveChangesAsync();

        var readerAdmin = fx.CreateReaderAdminService();
        var updateRes = await readerAdmin.UpdateAsync(new ReaderManageViewModel
        {
            Id = lockedTarget.Id,
            FullName = lockedTarget.FullName,
            Phone = lockedTarget.Phone,
            ReaderTypeId = rt.Id,
            CardExpiryDate = lockedTarget.CardExpiryDate,
            CardStatus = CardStatus.Locked,
            LockReason = "Vi phạm nội quy thư viện",
            Status = EntityStatus.Active
        });
        Assert.True(updateRes.IsSuccess, updateRes.Message);

        var updatedReader = await fx.Context.Readers.AsNoTracking().FirstAsync(r => r.Id == lockedTarget.Id);
        var updatedWaiterRes = await fx.Context.Reservations.AsNoTracking().FirstAsync(r => r.Id == pendingRes.Id);
        var releasedReadyRes = await fx.Context.Reservations.AsNoTracking().FirstAsync(r => r.Id == readyRes.Id);

        Assert.NotEqual(originalStamp, updatedReader.SecurityStamp);
        Assert.Equal(ReservationStatus.Cancelled, releasedReadyRes.ReservationStatus);
        Assert.Equal(ReservationStatus.Ready, updatedWaiterRes.ReservationStatus);
        Assert.Equal(copies[0].Id, updatedWaiterRes.AssignedCopyId);

        // Xóa mềm và xóa cứng khi không còn mượn/nợ phạt/phiếu
        // Đặt lại bản sao & bỏ liên kết lịch sử bằng test đọc giả sạch thứ 2
        var cleanReader = await fx.SeedReaderAsync(rt.Id, "SV603");
        Assert.True((await readerAdmin.SoftDeleteAsync(cleanReader.Id)).IsSuccess);
        Assert.True((await readerAdmin.HardDeleteAsync(cleanReader.Id, "XOA")).IsSuccess);
        Assert.False(await fx.Context.Readers.AnyAsync(r => r.Id == cleanReader.Id));
    }

    [Fact]
    public async Task DashboardAndReportServices_ShouldUseDefault2000Fine_HandleNullShelf_AndAggregateSqliteDatesCorrectly()
    {
        await using var fx = new SqliteTestDbFixture();
        // Cố ý KHÔNG seed LibrarySetting để kiểm chứng default 2000m (thay vì 5000m cũ)
        var staff = await fx.SeedStaffAsync();
        var rt = await fx.SeedReaderTypeAsync();
        var reader = await fx.SeedReaderAsync(rt.Id, "SV701");

        var (book, copies, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Báo cáo & Thống kê", copyCount: 1);
        // Cho ShelfId = null để test null-safe
        copies[0].ShelfId = null;
        await fx.Context.SaveChangesAsync();

        var due5DaysAgo = fx.DateTime.Today.AddDays(-5);
        var (slip, _) = await fx.SeedOpenLoanAsync(reader.Id, staff.Id, copies[0].Id, dueDate: due5DaysAgo, borrowedAt: fx.DateTime.UtcNow.AddDays(-10));

        var dashboard = await fx.CreateDashboardService().GetDashboardAsync(fx.DateTime.Today.Year);
        Assert.Equal(1, dashboard.TotalBorrowSlips);
        Assert.Equal(1, dashboard.BorrowingBooks);
        Assert.Equal(1, dashboard.MonthlyStatistics.Single(m => m.MonthNumber == fx.DateTime.Now.Month).BorrowCount);

        var reportSvc = fx.CreateReportService();
        var report = await reportSvc.GetReportDashboardAsync(new ReportFilter { Preset = "month" });
        Assert.Single(report.OverdueLoans);
        var overdueItem = report.OverdueLoans[0];
        Assert.Equal("—", overdueItem.ShelfLocation);
        Assert.Equal(5, overdueItem.OverdueDays);
        Assert.Equal(10000m, overdueItem.EstimatedFine); // 5 ngày * 2,000đ = 10,000đ
        Assert.Equal($"PM-{slip.BorrowedAt:yyyyMMdd}-{slip.Id.ToString("N")[..4].ToUpperInvariant()}", overdueItem.SlipCode);

        var csvBytes = await reportSvc.ExportOverdueLoansCsvAsync();
        var csv = System.Text.Encoding.UTF8.GetString(csvBytes);
        Assert.Contains(overdueItem.SlipCode, csv);
        Assert.Contains("10000", csv);
    }
}
