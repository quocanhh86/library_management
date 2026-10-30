using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using web_mvc_management.Data;
using web_mvc_management.Infrastructure;
using web_mvc_management.Services;
using Xunit;

namespace web_mvc_management.Tests.Services;

public class TestDateTimeProvider(DateTimeOffset utcNow) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
    public DateTimeOffset Now => UtcNow.ToOffset(TimeSpan.FromHours(7));
    public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);
}

public class OtpServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GenerateOtpAsync_ShouldCreateSixDigitCode_AndSaveToDb()
    {
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var service = new OtpService(context, timeProvider);

        var otp = await service.GenerateOtpAsync("reader@utc.edu.vn", "REGISTER");

        Assert.NotNull(otp);
        Assert.Equal(6, otp.Length);
        Assert.True(int.TryParse(otp, out _));

        var dbEntry = await context.Otps.FirstOrDefaultAsync(x => x.Email == "reader@utc.edu.vn");
        Assert.NotNull(dbEntry);
        Assert.Equal(otp, dbEntry.OtpCode);
        Assert.Equal("REGISTER", dbEntry.Purpose);
        Assert.False(dbEntry.IsUsed);
    }

    [Fact]
    public async Task VerifyOtpAsync_WithCorrectCode_ShouldSucceed_AndMarkUsed()
    {
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var service = new OtpService(context, timeProvider);

        var otp = await service.GenerateOtpAsync("student@utc.edu.vn", "READER_LOGIN");
        var result = await service.VerifyOtpAsync("student@utc.edu.vn", "READER_LOGIN", otp);

        Assert.True(result.IsSuccess);
        Assert.Equal(OtpStatus.Success, result.Status);

        var dbEntry = await context.Otps.FirstAsync(x => x.Email == "student@utc.edu.vn");
        Assert.True(dbEntry.IsUsed);
    }

    [Fact]
    public async Task VerifyOtpAsync_WithWrongCode_ShouldDecreaseRemainingAttempts()
    {
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var service = new OtpService(context, timeProvider);

        var otp = await service.GenerateOtpAsync("user@utc.edu.vn", "READER_LOGIN");
        var result = await service.VerifyOtpAsync("user@utc.edu.vn", "READER_LOGIN", "000000");

        Assert.False(result.IsSuccess);
        Assert.Equal(OtpStatus.Incorrect, result.Status);
        Assert.Equal(4, result.RemainingAttempts);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenFailed5Times_ShouldBurnOtp_SEC04()
    {
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var service = new OtpService(context, timeProvider);

        var otp = await service.GenerateOtpAsync("hacker@utc.edu.vn", "READER_LOGIN");

        // Fail 4 times
        for (int i = 0; i < 4; i++)
        {
            var res = await service.VerifyOtpAsync("hacker@utc.edu.vn", "READER_LOGIN", "000000");
            Assert.Equal(OtpStatus.Incorrect, res.Status);
        }

        // 5th failure -> BURN OTP!
        var burnResult = await service.VerifyOtpAsync("hacker@utc.edu.vn", "READER_LOGIN", "000000");

        Assert.True(burnResult.IsBurned);
        Assert.Equal(OtpStatus.Burned, burnResult.Status);

        var dbEntry = await context.Otps.FirstAsync(x => x.Email == "hacker@utc.edu.vn");
        Assert.True(dbEntry.IsUsed);

        // Subsequent attempt must return NotFound because code was burned
        var nextAttempt = await service.VerifyOtpAsync("hacker@utc.edu.vn", "READER_LOGIN", otp);
        Assert.Equal(OtpStatus.NotFound, nextAttempt.Status);
    }

    [Fact]
    public async Task VerifyOtpAsync_WithWrongPurpose_ShouldReturnNotFound_SEC12()
    {
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var service = new OtpService(context, timeProvider);

        var otp = await service.GenerateOtpAsync("victim@utc.edu.vn", "REGISTER");

        // Attacker attempts to use REGISTER OTP for STAFF_LOGIN
        var crossDomainResult = await service.VerifyOtpAsync("victim@utc.edu.vn", "STAFF_LOGIN", otp);

        Assert.Equal(OtpStatus.NotFound, crossDomainResult.Status);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenExpired_ShouldReturnExpired()
    {
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var service = new OtpService(context, timeProvider);

        var otp = await service.GenerateOtpAsync("student@utc.edu.vn", "READER_LOGIN", TimeSpan.FromMinutes(5));

        // Fast forward 6 minutes
        timeProvider.UtcNow = timeProvider.UtcNow.AddMinutes(6);

        var result = await service.VerifyOtpAsync("student@utc.edu.vn", "READER_LOGIN", otp);

        Assert.Equal(OtpStatus.Expired, result.Status);
    }
}
