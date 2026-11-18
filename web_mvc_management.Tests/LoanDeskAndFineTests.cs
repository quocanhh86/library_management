using Microsoft.EntityFrameworkCore;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Models.ViewModels.Circulation;
using web_mvc_management.Tests.TestInfrastructure;
using Xunit;
using static web_mvc_management.Constant.LibraryConstants;

namespace web_mvc_management.Tests;

public class LoanDeskAndFineTests
{
    [Fact]
    public async Task CreateBorrowSlip_And_ReturnGood_ShouldHonorTriggerContract_PromoteReservation_AndUse4HexSlipCode()
    {
        await using var fx = new SqliteTestDbFixture();
        await fx.SeedSettingAsync(holdHours: 48);
        var staff = await fx.SeedStaffAsync("thuthu01");
        fx.UserContext.SetUser(staff.Id, staff.Username, staff.Role);

        var rt = await fx.SeedReaderTypeAsync(maxBorrow: 3, loanDays: 14);
        var borrower = await fx.SeedReaderAsync(rt.Id, "SV01");
        var waiter = await fx.SeedReaderAsync(rt.Id, "SV02");

        var (book, copies, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Thiết kế hướng đối tượng", copyCount: 1);

        var loanDesk = fx.CreateLoanDeskService();
        var borrowRes = await loanDesk.CreateBorrowSlipAsync(new BorrowSubmitViewModel
        {
            ReaderId = borrower.Id,
            Barcodes = [copies[0].Barcode]
        });

        Assert.True(borrowRes.IsSuccess, borrowRes.Message);
        var slipId = borrowRes.Data;

        // Trigger đã chuyển bản sao sang BORROWED sau khi insert BorrowDetail
        var borrowedCopy = await fx.Context.BookCopies.AsNoTracking().FirstAsync(c => c.Id == copies[0].Id);
        Assert.Equal(CopyStatus.Borrowed, borrowedCopy.CopyStatus);

        // Kiểm tra SlipCode chuẩn 4 ký tự hex (PM-yyyyMMdd-XXXX)
        var detailVm = await loanDesk.GetBorrowSlipDetailAsync(slipId);
        Assert.NotNull(detailVm);
        var expectedCode = $"PM-{detailVm.BorrowedAt:yyyyMMdd}-{slipId.ToString("N")[..4].ToUpperInvariant()}";
        Assert.Equal(expectedCode, detailVm.SlipCode);
        Assert.Matches(@"^PM-\d{8}-[A-F0-9]{4}$", detailVm.SlipCode);

        // Người thứ hai đặt trước sách
        var resSvc = fx.CreateReservationService();
        var reserveResult = await resSvc.CreateReservationAsync(new ReservationCreateViewModel
        {
            CardNo = waiter.CardNo,
            BookId = book.Id
        });
        Assert.True(reserveResult.IsSuccess, reserveResult.Message);

        // Trả sách nguyên vẹn -> SaveChanges (trigger -> AVAILABLE) trước -> QueueEngine chuyển ngay sang RESERVED cho SV02
        var borrowDetailId = detailVm.Items[0].DetailId;
        var returnRes = await loanDesk.ReturnAsync(new ReturnSubmitViewModel
        {
            ReaderId = borrower.Id,
            Items = [new ReturnItemViewModel { DetailId = borrowDetailId, Selected = true, Condition = ReturnCondition.Good }]
        });
        Assert.True(returnRes.IsSuccess, returnRes.Message);

        var finalCopy = await fx.Context.BookCopies.AsNoTracking().FirstAsync(c => c.Id == copies[0].Id);
        var waiterRes = await fx.Context.Reservations.AsNoTracking().FirstAsync(r => r.ReaderId == waiter.Id);

        Assert.Equal(CopyStatus.Reserved, finalCopy.CopyStatus);
        Assert.Equal(ReservationStatus.Ready, waiterRes.ReservationStatus);
        Assert.Equal(copies[0].Id, waiterRes.AssignedCopyId);
    }

    [Fact]
    public async Task ReturnOverdueAndDamagedOrLost_ShouldComputeAccurateFines_AndPayOrWaiveFineWorks()
    {
        await using var fx = new SqliteTestDbFixture();
        await fx.SeedSettingAsync(overduePerDay: 2000m, damagedPercent: 50, lostPercent: 100);
        var staff = await fx.SeedStaffAsync("admin01", AppRoles.Admin);
        fx.UserContext.SetUser(staff.Id, staff.Username, staff.Role);

        var rt = await fx.SeedReaderTypeAsync(loanDays: 10);
        var reader = await fx.SeedReaderAsync(rt.Id, "SV777");
        var (_, copies, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Mạng máy tính", copyCount: 2, copyPrice: 120000m);

        var due3DaysAgo = fx.DateTime.Today.AddDays(-3);
        var (_, detailDamaged) = await fx.SeedOpenLoanAsync(reader.Id, staff.Id, copies[0].Id, dueDate: due3DaysAgo);
        var (_, detailLost) = await fx.SeedOpenLoanAsync(reader.Id, staff.Id, copies[1].Id, dueDate: fx.DateTime.Today.AddDays(5));

        var loanDesk = fx.CreateLoanDeskService();
        var returnRes = await loanDesk.ReturnAsync(new ReturnSubmitViewModel
        {
            ReaderId = reader.Id,
            Items =
            [
                new ReturnItemViewModel { DetailId = detailDamaged.Id, Selected = true, Condition = ReturnCondition.Damaged, Note = "Rách bìa" },
                new ReturnItemViewModel { DetailId = detailLost.Id, Selected = true, Condition = ReturnCondition.Lost }
            ]
        });
        Assert.True(returnRes.IsSuccess, returnRes.Message);

        var copy0 = await fx.Context.BookCopies.AsNoTracking().FirstAsync(c => c.Id == copies[0].Id);
        var copy1 = await fx.Context.BookCopies.AsNoTracking().FirstAsync(c => c.Id == copies[1].Id);
        Assert.Equal(CopyStatus.Damaged, copy0.CopyStatus);
        Assert.Equal(CopyStatus.Lost, copy1.CopyStatus);

        var fines = await fx.Context.Fines.AsNoTracking().ToListAsync();
        // 3 ngày trễ * 2000 = 6000; 50% * 120000 = 60000; 100% * 120000 = 120000
        Assert.Equal(3, fines.Count);
        Assert.Equal(6000m, fines.Single(f => f.BorrowDetailId == detailDamaged.Id && f.FineType == FineType.Overdue).Amount);
        Assert.Equal(60000m, fines.Single(f => f.BorrowDetailId == detailDamaged.Id && f.FineType == FineType.Damaged).Amount);
        Assert.Equal(120000m, fines.Single(f => f.BorrowDetailId == detailLost.Id && f.FineType == FineType.Lost).Amount);

        var fineSvc = fx.CreateFineService();
        var fineOverdue = fines.First(f => f.FineType == FineType.Overdue);
        var fineLost = fines.First(f => f.FineType == FineType.Lost);

        // Thu tiền phạt & Miễn phạt
        var payRes = await fineSvc.PayFineAsync(fineOverdue.Id, PaymentMethod.Cash);
        Assert.True(payRes.IsSuccess);
        Assert.False((await fineSvc.PayFineAsync(fineOverdue.Id, PaymentMethod.Cash)).IsSuccess); // Không thu trùng

        Assert.False((await fineSvc.WaiveFineAsync(fineLost.Id, "   ")).IsSuccess); // Bắt buộc có lý do
        var waiveRes = await fineSvc.WaiveFineAsync(fineLost.Id, "Sinh viên hoàn cảnh khó khăn");
        Assert.True(waiveRes.IsSuccess);

        var page = await fineSvc.GetFinesPageAsync(new FineFilter());
        Assert.Equal(60000m, page.UnpaidTotal);
    }

    [Fact]
    public async Task CancelBorrowSlip_Within24Hours_ShouldRestoreCopyAvailability_AndRejectAfter24Hours()
    {
        await using var fx = new SqliteTestDbFixture();
        await fx.SeedSettingAsync();
        var staff = await fx.SeedStaffAsync();
        fx.UserContext.SetUser(staff.Id, staff.Username, staff.Role);

        var rt = await fx.SeedReaderTypeAsync();
        var reader = await fx.SeedReaderAsync(rt.Id, "SV888");
        var (_, copies, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Hóa đại cương", copyCount: 2);

        var (slipRecent, _) = await fx.SeedOpenLoanAsync(reader.Id, staff.Id, copies[0].Id, borrowedAt: fx.DateTime.UtcNow.AddHours(-2));
        var (slipOld, _) = await fx.SeedOpenLoanAsync(reader.Id, staff.Id, copies[1].Id, borrowedAt: fx.DateTime.UtcNow.AddHours(-25));

        var loanDesk = fx.CreateLoanDeskService();

        var oldCancel = await loanDesk.CancelBorrowSlipAsync(slipOld.Id, "Nhầm phiếu");
        Assert.False(oldCancel.IsSuccess);
        Assert.Equal(ErrorCode.BorrowSlipCannotCancelExpired, oldCancel.Code);

        var recentCancel = await loanDesk.CancelBorrowSlipAsync(slipRecent.Id, "Khách đổi ý");
        Assert.True(recentCancel.IsSuccess);

        var cancelledSlip = await fx.Context.BorrowSlips.AsNoTracking().FirstAsync(s => s.Id == slipRecent.Id);
        var restoredCopy = await fx.Context.BookCopies.AsNoTracking().FirstAsync(c => c.Id == copies[0].Id);
        Assert.Equal(EntityStatus.Deleted, cancelledSlip.Status);
        Assert.Equal(CopyStatus.Available, restoredCopy.CopyStatus);
    }
}
