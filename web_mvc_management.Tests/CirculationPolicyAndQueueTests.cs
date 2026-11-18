using Microsoft.EntityFrameworkCore;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Models.ViewModels.Circulation;
using web_mvc_management.Tests.TestInfrastructure;
using Xunit;
using static web_mvc_management.Constant.LibraryConstants;

namespace web_mvc_management.Tests;

public class CirculationPolicyAndQueueTests
{
    [Fact]
    public async Task UnpaidFineUnderMaxLimit_ShouldAllowBorrowRenewAndReserve_OnBothPortals()
    {
        await using var fx = new SqliteTestDbFixture();
        await fx.SeedSettingAsync(overduePerDay: 2000m, maxUnpaidFine: 50000m);
        var staff = await fx.SeedStaffAsync();
        fx.UserContext.SetUser(staff.Id, staff.Username, staff.Role);

        var rt = await fx.SeedReaderTypeAsync(maxBorrow: 5, loanDays: 14, maxRenewals: 2, renewDays: 7);
        var reader = await fx.SeedReaderAsync(rt.Id, "SV20260001");

        // Tạo 1 cuốn đang mượn có nợ phạt 40,000đ (< 50,000đ hạn mức)
        var (book1, copies1, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Sách 1", copyCount: 1);
        var (_, detail1) = await fx.SeedOpenLoanAsync(reader.Id, staff.Id, copies1[0].Id);
        await fx.SeedFineAsync(detail1.Id, 40000m);

        // Đầu sách thứ 2 còn bản sẵn sàng để test mượn mới
        var (book2, copies2, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Sách 2", copyCount: 1);

        // Đầu sách thứ 3 chỉ có 1 bản đang được độc giả khác mượn để test đặt trước
        var otherReader = await fx.SeedReaderAsync(rt.Id, "SV20260002");
        var (book3, copies3, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Sách 3", copyCount: 1);
        await fx.SeedOpenLoanAsync(otherReader.Id, staff.Id, copies3[0].Id);

        var loanDesk = fx.CreateLoanDeskService();
        var staffReservation = fx.CreateReservationService();
        var readerPortal = fx.CreateReaderPortalService();

        // 1. Mượn tại quầy vẫn được phép
        var borrowRes = await loanDesk.CreateBorrowSlipAsync(new BorrowSubmitViewModel
        {
            ReaderId = reader.Id,
            Barcodes = [copies2[0].Barcode]
        });
        Assert.True(borrowRes.IsSuccess, borrowRes.Message);

        // 2. Gia hạn tại quầy VÀ tự gia hạn trên cổng độc giả đều được phép
        var renewDesk = await loanDesk.RenewAsync(detail1.Id);
        Assert.True(renewDesk.IsSuccess, renewDesk.Message);

        var renewPortal = await readerPortal.RenewLoanAsync(reader.Id, detail1.Id);
        Assert.True(renewPortal.IsSuccess, renewPortal.Message);

        // 3. Đặt trước tại quầy được phép (40k < 50k)
        var reserveDesk = await staffReservation.CreateReservationAsync(new ReservationCreateViewModel
        {
            CardNo = reader.CardNo,
            BookId = book3.Id
        });
        Assert.True(reserveDesk.IsSuccess, reserveDesk.Message);
    }

    [Fact]
    public async Task UnpaidFineAtOrAboveMaxLimit_ShouldBlockBorrowRenewAndReserve_OnBothPortals()
    {
        await using var fx = new SqliteTestDbFixture();
        await fx.SeedSettingAsync(overduePerDay: 2000m, maxUnpaidFine: 50000m);
        var staff = await fx.SeedStaffAsync();
        fx.UserContext.SetUser(staff.Id, staff.Username, staff.Role);

        var rt = await fx.SeedReaderTypeAsync(maxBorrow: 5);
        var reader = await fx.SeedReaderAsync(rt.Id, "SV20260010");
        var otherReader = await fx.SeedReaderAsync(rt.Id, "SV20260011");

        var (_, copies1, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Sách phạt 50k", copyCount: 1);
        var (_, detail1) = await fx.SeedOpenLoanAsync(reader.Id, staff.Id, copies1[0].Id);
        await fx.SeedFineAsync(detail1.Id, 50000m);

        var (_, copies2, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Sách định mượn", copyCount: 1);
        var (book3, copies3, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Sách định đặt", copyCount: 1);
        await fx.SeedOpenLoanAsync(otherReader.Id, staff.Id, copies3[0].Id);

        var loanDesk = fx.CreateLoanDeskService();
        var staffReservation = fx.CreateReservationService();
        var readerPortal = fx.CreateReaderPortalService();

        var borrowRes = await loanDesk.CreateBorrowSlipAsync(new BorrowSubmitViewModel
        {
            ReaderId = reader.Id,
            Barcodes = [copies2[0].Barcode]
        });
        Assert.False(borrowRes.IsSuccess);
        Assert.Equal(ErrorCode.ReaderHasUnpaidFines, borrowRes.Code);

        var renewDesk = await loanDesk.RenewAsync(detail1.Id);
        Assert.False(renewDesk.IsSuccess);
        Assert.Equal(ErrorCode.ReaderHasUnpaidFines, renewDesk.Code);

        var renewPortal = await readerPortal.RenewLoanAsync(reader.Id, detail1.Id);
        Assert.False(renewPortal.IsSuccess);
        Assert.Equal(ErrorCode.ReaderHasUnpaidFines, renewPortal.Code);

        var reserveDesk = await staffReservation.CreateReservationAsync(new ReservationCreateViewModel
        {
            CardNo = reader.CardNo,
            BookId = book3.Id
        });
        Assert.False(reserveDesk.IsSuccess);
        Assert.Equal(ErrorCode.ReaderHasUnpaidFines, reserveDesk.Code);

        var reservePortal = await readerPortal.CreateReservationAsync(reader.Id, book3.Id);
        Assert.False(reservePortal.IsSuccess);
        Assert.Equal(ErrorCode.ReaderHasUnpaidFines, reservePortal.Code);
    }

    [Fact]
    public async Task QueueEngine_FifoShouldSkipIneligibleFirstReader_AndPromoteSecondReader_ThenReleaseToAvailable()
    {
        await using var fx = new SqliteTestDbFixture();
        await fx.SeedSettingAsync(holdHours: 24);
        var rt = await fx.SeedReaderTypeAsync();

        var reader1Locked = await fx.SeedReaderAsync(rt.Id, "SV001", cardStatus: CardStatus.Locked);
        var reader2Eligible = await fx.SeedReaderAsync(rt.Id, "SV002", cardStatus: CardStatus.Active);
        var (book, copies, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Thuật toán nâng cao", copyCount: 1);

        var res1 = new Reservation
        {
            Id = Guid.NewGuid(),
            BookId = book.Id,
            ReaderId = reader1Locked.Id,
            ReservationStatus = ReservationStatus.Pending,
            ReservedAt = fx.DateTime.UtcNow.AddHours(-3),
            Status = EntityStatus.Active
        };
        var res2 = new Reservation
        {
            Id = Guid.NewGuid(),
            BookId = book.Id,
            ReaderId = reader2Eligible.Id,
            ReservationStatus = ReservationStatus.Pending,
            ReservedAt = fx.DateTime.UtcNow.AddHours(-2),
            Status = EntityStatus.Active
        };
        await fx.Context.Reservations.AddRangeAsync(res1, res2);
        await fx.Context.SaveChangesAsync();

        var queue = fx.CreateQueueEngine();
        await queue.AssignCopyToNextPendingOrReleaseAsync(copies[0]);

        var updatedCopy = await fx.Context.BookCopies.AsNoTracking().FirstAsync(c => c.Id == copies[0].Id);
        var updatedRes1 = await fx.Context.Reservations.AsNoTracking().FirstAsync(r => r.Id == res1.Id);
        var updatedRes2 = await fx.Context.Reservations.AsNoTracking().FirstAsync(r => r.Id == res2.Id);

        // Thẻ khóa phải bị bỏ qua, SV002 được giữ chỗ READY trong 24h
        Assert.Equal(CopyStatus.Reserved, updatedCopy.CopyStatus);
        Assert.Equal(ReservationStatus.Pending, updatedRes1.ReservationStatus);
        Assert.Equal(ReservationStatus.Ready, updatedRes2.ReservationStatus);
        Assert.Equal(copies[0].Id, updatedRes2.AssignedCopyId);
        Assert.Equal(fx.DateTime.UtcNow.AddHours(24), updatedRes2.ExpiresAt);

        // Tiến thời gian quá 24h -> hết hạn giữ chỗ của SV002, không còn ai hợp lệ -> bản sao về AVAILABLE
        fx.DateTime.AdvanceHours(25);
        await queue.ExpireOverdueHoldsAsync();

        var expiredRes2 = await fx.Context.Reservations.AsNoTracking().FirstAsync(r => r.Id == res2.Id);
        var releasedCopy = await fx.Context.BookCopies.AsNoTracking().FirstAsync(c => c.Id == copies[0].Id);
        Assert.Equal(ReservationStatus.Expired, expiredRes2.ReservationStatus);
        Assert.Equal(CopyStatus.Available, releasedCopy.CopyStatus);
    }

    [Fact]
    public async Task ExpireOverdueHolds_ShouldCascadeFromExpiredReader_ToNextEligiblePendingReader()
    {
        await using var fx = new SqliteTestDbFixture();
        await fx.SeedSettingAsync(holdHours: 12);
        var rt = await fx.SeedReaderTypeAsync();

        var r1 = await fx.SeedReaderAsync(rt.Id, "SV101");
        var r2 = await fx.SeedReaderAsync(rt.Id, "SV102");
        var (book, copies, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Giải tích", copyCount: 1);

        copies[0].CopyStatus = CopyStatus.Reserved;
        var resReady = new Reservation
        {
            Id = Guid.NewGuid(),
            BookId = book.Id,
            ReaderId = r1.Id,
            AssignedCopyId = copies[0].Id,
            ReservationStatus = ReservationStatus.Ready,
            ReservedAt = fx.DateTime.UtcNow.AddDays(-2),
            ReadyAt = fx.DateTime.UtcNow.AddHours(-13),
            ExpiresAt = fx.DateTime.UtcNow.AddHours(-1),
            Status = EntityStatus.Active
        };
        var resPending = new Reservation
        {
            Id = Guid.NewGuid(),
            BookId = book.Id,
            ReaderId = r2.Id,
            ReservationStatus = ReservationStatus.Pending,
            ReservedAt = fx.DateTime.UtcNow.AddDays(-1),
            Status = EntityStatus.Active
        };
        await fx.Context.Reservations.AddRangeAsync(resReady, resPending);
        await fx.Context.SaveChangesAsync();

        await fx.CreateQueueEngine().ExpireOverdueHoldsAsync();

        var oldHold = await fx.Context.Reservations.AsNoTracking().FirstAsync(r => r.Id == resReady.Id);
        var nextHold = await fx.Context.Reservations.AsNoTracking().FirstAsync(r => r.Id == resPending.Id);
        var copy = await fx.Context.BookCopies.AsNoTracking().FirstAsync(c => c.Id == copies[0].Id);

        Assert.Equal(ReservationStatus.Expired, oldHold.ReservationStatus);
        Assert.Equal(ReservationStatus.Ready, nextHold.ReservationStatus);
        Assert.Equal(copies[0].Id, nextHold.AssignedCopyId);
        Assert.Equal(CopyStatus.Reserved, copy.CopyStatus);
    }
}
