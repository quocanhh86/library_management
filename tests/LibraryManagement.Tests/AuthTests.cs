using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Services;
using web_mvc_management.Util;

namespace LibraryManagement.Tests;

public class AuthTests : IAsyncLifetime
{
    private readonly TestDatabase f = new();
    private readonly CapturedEmail mail = new();
    private AuthService Auth = null!;
    private const string Password = "Testonly!123";
    public async Task InitializeAsync()
    {
        await f.InitializeAsync();
        f.Reader.PasswordHash = PasswordHasher.HashPassword(Password);
        await f.Db.SaveChangesAsync();
        Auth = new(f.Uow, new OtpService(f.Uow, f.Clock), mail, new IpRateLimiterService(f.Clock), f.Clock, NullLogger<AuthService>.Instance);
    }
    public Task DisposeAsync() => f.DisposeAsync().AsTask();
    [Fact] public async Task ReaderPasswordAndOtpLoginRejectsReplay()
    {
        var first = await Auth.InitiateReaderLoginAsync(new() { UsernameOrEmail = f.Reader.Username, Password = Password }, "test");
        Assert.True(first.Succeeded);
        Assert.True(first.Data!.RequiresOtp);
        var completed = await Auth.CompleteReaderLoginOtpAsync(f.Reader.Email, mail.Code, "test");
        Assert.True(completed.Succeeded);
        Assert.True(completed.Data!.IsInRole(AppRoles.Reader));
        Assert.False((await Auth.CompleteReaderLoginOtpAsync(f.Reader.Email, mail.Code, "test")).Succeeded);
    }
    [Fact] public async Task StaffEmailOtpLoginUsesStaffRole()
    {
        var staff = await f.Db.Staff.SingleAsync();
        Assert.True((await Auth.InitiateStaffLoginAsync(new() { Email = staff.Email }, "test")).Succeeded);
        var result = await Auth.CompleteStaffLoginOtpAsync(staff.Email, mail.Code, "test");
        Assert.True(result.Succeeded);
        Assert.True(result.Data!.IsInRole(AppRoles.Admin));
    }
    [Fact] public async Task FiveWrongPasswordsLockAccountAndRotateSecurityStamp()
    {
        var stamp = f.Reader.SecurityStamp;
        for (int i = 0; i < 5; i++) Assert.False((await Auth.InitiateReaderLoginAsync(new() { UsernameOrEmail = f.Reader.Username, Password = "wrong" }, "test")).Succeeded);
        Assert.NotEqual(stamp, f.Reader.SecurityStamp);
        Assert.True(f.Reader.LockedUntil > f.Clock.UtcNow);
        Assert.False((await Auth.InitiateReaderLoginAsync(new() { UsernameOrEmail = f.Reader.Username, Password = Password }, "test")).Succeeded);
        f.Clock.UtcNow = f.Clock.UtcNow.AddMinutes(16);
        Assert.True((await Auth.InitiateReaderLoginAsync(new() { UsernameOrEmail = f.Reader.Username, Password = Password }, "test")).Succeeded);
    }
    [Fact] public async Task RegistrationCreatesCardAndRejectsDuplicate()
    {
        var model = new web_mvc_management.Models.ViewModels.Auth.ReaderRegisterViewModel { Username = "newreader", Email = "newreader@example.invalid", FullName = "New Reader", Phone = "0912345678", Password = Password, ConfirmPassword = Password, ReaderTypeId = f.Reader.ReaderTypeId };
        Assert.True((await Auth.SendRegisterOtpAsync(model, "test")).Succeeded);
        var result = await Auth.CompleteRegistrationAsync(model, mail.Code, "test");
        Assert.True(result.Succeeded, result.Message);
        Assert.NotEmpty(result.Data!.CardNo);
        Assert.True(PasswordHasher.VerifyPassword(Password, result.Data.PasswordHash));
        Assert.False((await Auth.SendRegisterOtpAsync(model, "test")).Succeeded);
    }
    [Fact] public async Task ReaderDisabledBetweenPasswordAndOtpCannotLogin()
    {
        Assert.True((await Auth.InitiateReaderLoginAsync(new() { UsernameOrEmail = f.Reader.Username, Password = Password }, "test")).Succeeded);
        f.Reader.Status = EntityStatus.Deleted;
        await f.Db.SaveChangesAsync();
        Assert.False((await Auth.CompleteReaderLoginOtpAsync(f.Reader.Email, mail.Code, "test")).Succeeded);
    }
    [Fact] public void IpLimitSeparatesActionsAndExpires()
    {
        var limiter = new IpRateLimiterService(f.Clock);
        for (int i = 0; i < 5; i++) Assert.True(limiter.IsAllowed("test", "OTP", 5));
        Assert.False(limiter.IsAllowed("test", "OTP", 5));
        Assert.True(limiter.IsAllowed("test", "LOGIN", 5));
        f.Clock.UtcNow = f.Clock.UtcNow.AddSeconds(61);
        Assert.True(limiter.IsAllowed("test", "OTP", 5));
    }
    [Fact] public async Task StaffDisabledWhileOtpPendingCannotLogin()
    {
        var staff = await f.Db.Staff.SingleAsync();
        Assert.True((await Auth.InitiateStaffLoginAsync(new() { Email = staff.Email }, "test")).Succeeded);
        staff.Status = EntityStatus.Deleted;
        await f.Db.SaveChangesAsync();
        Assert.False((await Auth.CompleteStaffLoginOtpAsync(staff.Email, mail.Code, "test")).Succeeded);
    }
    private sealed class CapturedEmail : IEmailService
    {
        public string Code { get; private set; } = "";
        public int GetRemainingCooldownSeconds(string email) => 0;
        public Task<ServiceResult> SendOtpEmailAsync(string toEmail, string otpCode, string purpose, CancellationToken cancellationToken = default)
        {
            Code = otpCode;
            return Task.FromResult(ServiceResult.Success());
        }
    }
}
