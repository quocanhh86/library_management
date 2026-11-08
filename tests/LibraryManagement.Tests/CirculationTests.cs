using Microsoft.EntityFrameworkCore;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Models.ViewModels.Circulation;
using static web_mvc_management.Constant.LibraryConstants;

namespace LibraryManagement.Tests;

public class CirculationTests : IAsyncLifetime
{
    private readonly TestDatabase f = new();
    public Task InitializeAsync() => f.InitializeAsync();
    public Task DisposeAsync() => f.DisposeAsync().AsTask();
    private Task<web_mvc_management.Common.ServiceResult<Guid>> Borrow(params string[] codes) => f.Circulation.CreateBorrowSlipAsync(new() { ReaderId = f.Reader.Id, Barcodes = codes.ToList() });
    private async Task<Guid> BorrowOne()
    {
        var result = await Borrow(f.Copy.Barcode);
        Assert.True(result.Succeeded, result.Message);
        return await f.Db.BorrowDetails.Select(d => d.Id).SingleAsync();
    }
    [Fact] public async Task BorrowRenewReturnAndRejectDuplicateReturn()
    {
        var id = await BorrowOne();
        Assert.Equal(CopyStatus.Borrowed, (await f.Db.BookCopies.AsNoTracking().SingleAsync()).CopyStatus);
        Assert.True((await f.Circulation.RenewAsync(id)).Succeeded);
        Assert.True((await f.Circulation.RenewAsync(id)).Succeeded);
        Assert.False((await f.Circulation.RenewAsync(id)).Succeeded);
        var input = new ReturnSubmitViewModel { ReaderId = f.Reader.Id, Items = [new() { DetailId = id, Selected = true }] };
        Assert.True((await f.Circulation.ReturnAsync(input)).Succeeded);
        Assert.False((await f.Circulation.ReturnAsync(input)).Succeeded);
        Assert.Equal(CopyStatus.Available, (await f.Db.BookCopies.AsNoTracking().SingleAsync()).CopyStatus);
        Assert.Empty(await f.Db.Fines.ToListAsync());
    }
    [Theory]
    [InlineData("LOCKED")]
    [InlineData("EXPIRED")]
    public async Task InvalidCardCannotBorrow(string status)
    {
        f.Reader.CardStatus = status;
        await f.Db.SaveChangesAsync();
        Assert.False((await Borrow(f.Copy.Barcode)).Succeeded);
        Assert.Empty(await f.Db.BorrowSlips.ToListAsync());
    }
    [Fact] public async Task EmptyDuplicateMissingAndUnavailableBarcodesAreRejected()
    {
        Assert.False((await Borrow()).Succeeded);
        Assert.False((await Borrow(f.Copy.Barcode, f.Copy.Barcode.ToLowerInvariant())).Succeeded);
        Assert.False((await Borrow("missing")).Succeeded);
        await BorrowOne();
        Assert.False((await Borrow(f.Copy.Barcode)).Succeeded);
        Assert.Single(await f.Db.BorrowDetails.ToListAsync());
    }
    [Theory]
    [InlineData("GOOD", 6000)]
    [InlineData("DAMAGED", 56000)]
    [InlineData("LOST", 156000)]
    public async Task LateReturnsCalculateFinesAndPaymentIsIdempotent(string condition, decimal amount)
    {
        var id = await BorrowOne();
        f.Clock.UtcNow = f.Clock.UtcNow.AddDays(17);
        Assert.False((await f.Circulation.RenewAsync(id)).Succeeded);
        Assert.True((await f.Circulation.ReturnAsync(new() { ReaderId = f.Reader.Id, Items = [new() { DetailId = id, Selected = true, Condition = condition }] })).Succeeded);
        Assert.Equal(amount, await f.Db.Fines.SumAsync(x => x.Amount));
        var fine = await f.Db.Fines.FirstAsync();
        Assert.False((await f.Circulation.PayFineAsync(fine.Id, "INVALID")).Succeeded);
        Assert.True((await f.Circulation.PayFineAsync(fine.Id, PaymentMethod.Cash)).Succeeded);
        Assert.False((await f.Circulation.PayFineAsync(fine.Id, PaymentMethod.Cash)).Succeeded);
    }
    [Fact] public async Task CancelRestoresCopyAndHidesSlip()
    {
        var id = (await Borrow(f.Copy.Barcode)).Data;
        Assert.False((await f.Circulation.CancelBorrowSlipAsync(id, "")).Succeeded);
        Assert.True((await f.Circulation.CancelBorrowSlipAsync(id, "Entered by mistake")).Succeeded);
        Assert.Null(await f.Circulation.GetBorrowSlipDetailAsync(id));
        Assert.Equal(CopyStatus.Available, (await f.Db.BookCopies.AsNoTracking().SingleAsync()).CopyStatus);
        Assert.False(await f.Db.BorrowDetails.AsNoTracking().AnyAsync(d => d.SlipId == id && d.Status != EntityStatus.Deleted));
    }
    [Fact] public async Task ReservationQueueReceivesReturnedCopyAndCancellationReleasesIt()
    {
        var detail = await BorrowOne();
        var reader2 = new Reader { Id = Guid.NewGuid(), ReaderTypeId = f.Reader.ReaderTypeId, Username = "r2", Email = "r2@example.invalid", FullName = "Reader 2", PasswordHash = "test", CardNo = "TEST002", CardIssuedDate = f.Clock.Today, CardExpiryDate = f.Clock.Today.AddYears(1), CardStatus = CardStatus.Active };
        f.Db.Add(reader2);
        await f.Db.SaveChangesAsync();
        var request = new ReservationCreateViewModel { CardNo = reader2.CardNo, BookId = f.Copy.BookId };
        Assert.True((await f.Circulation.CreateReservationAsync(request)).Succeeded);
        Assert.False((await f.Circulation.CreateReservationAsync(request)).Succeeded);
        Assert.False((await f.Circulation.RenewAsync(detail)).Succeeded);
        Assert.True((await f.Circulation.ReturnAsync(new() { ReaderId = f.Reader.Id, Items = [new() { DetailId = detail, Selected = true }] })).Succeeded);
        var reservation = await f.Db.Reservations.AsNoTracking().SingleAsync();
        Assert.Equal(ReservationStatus.Ready, reservation.ReservationStatus);
        Assert.Equal(f.Copy.Id, reservation.AssignedCopyId);
        Assert.True((await f.Circulation.CancelReservationAsync(reservation.Id)).Succeeded);
        Assert.Equal(CopyStatus.Available, (await f.Db.BookCopies.AsNoTracking().SingleAsync()).CopyStatus);
    }
    [Fact] public async Task WrongReaderOrConditionCannotReturn()
    {
        var id = await BorrowOne();
        Assert.False((await f.Circulation.ReturnAsync(new() { ReaderId = Guid.NewGuid(), Items = [new() { DetailId = id, Selected = true }] })).Succeeded);
        Assert.False((await f.Circulation.ReturnAsync(new() { ReaderId = f.Reader.Id, Items = [new() { DetailId = id, Selected = true, Condition = "INVALID" }] })).Succeeded);
        Assert.Null((await f.Db.BorrowDetails.AsNoTracking().SingleAsync()).ReturnedAt);
    }
    [Fact] public async Task CancellationRollsBackAllCopiesWhenSecondSaveFails()
    {
        var second = new BookCopy { Id = Guid.NewGuid(), BookId = f.Copy.BookId, Barcode = "SECOND", AcquiredDate = f.Clock.Today };
        f.Db.Add(second);
        await f.Db.SaveChangesAsync();
        var result = await Borrow(f.Copy.Barcode, second.Barcode);
        Assert.True(result.Succeeded);
        f.Failure.SavesUntilFailure = 2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Circulation.CancelBorrowSlipAsync(result.Data, "Mistake"));
        await using var fresh = f.NewContext();
        Assert.All(await fresh.BookCopies.ToListAsync(), copy => Assert.Equal(CopyStatus.Borrowed, copy.CopyStatus));
        Assert.All(await fresh.BorrowDetails.ToListAsync(), detail => Assert.Equal(EntityStatus.Active, detail.Status));
        Assert.Equal(EntityStatus.Active, (await fresh.BorrowSlips.SingleAsync()).Status);
    }
}
