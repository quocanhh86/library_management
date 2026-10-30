using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Data;
using web_mvc_management.Entity;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Auth;
using web_mvc_management.Services;
using web_mvc_management.Util;
using Xunit;

namespace web_mvc_management.Tests.Services;

public class FakeEmailService : IEmailService
{
    public List<(string To, string Otp, string Purpose)> SentOtps { get; } = new();

    public Task<ServiceResult> SendOtpEmailAsync(string toEmail, string otpCode, string purpose, CancellationToken cancellationToken = default)
    {
        SentOtps.Add((toEmail, otpCode, purpose));
        return Task.FromResult(ServiceResult.Success("Đã gửi mã xác thực"));
    }

    public int GetRemainingCooldownSeconds(string email) => 0;
}

public class FakeIpRateLimiterService : IIpRateLimiterService
{
    public bool ShouldAllow { get; set; } = true;

    public string GetClientIp(Microsoft.AspNetCore.Http.HttpContext httpContext) => "127.0.0.1";

    public bool IsAllowed(string ipAddress, string actionType, int maxRequestsPerMinute) => ShouldAllow;

    public void Reset(string ipAddress, string actionType) { }
}

public class AuthServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task SendRegisterOtpAsync_ValidInput_GeneratesOtpAndSendsEmail()
    {
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);
        var emailService = new FakeEmailService();
        var rateLimiter = new FakeIpRateLimiterService();
        var logger = NullLogger<AuthService>.Instance;

        var readerType = new ReaderType
        {
            Id = Guid.NewGuid(),
            Code = "STUDENT",
            Name = "Sinh Viên",
            Status = EntityStatus.Active
        };
        context.ReaderTypes.Add(readerType);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, otpService, emailService, rateLimiter, timeProvider, logger);

        var model = new ReaderRegisterViewModel
        {
            FullName = "Nguyen Van A",
            Username = "nguyenvana",
            Email = "vana@utc.edu.vn",
            Phone = "0987654321",
            ReaderTypeId = readerType.Id,
            Password = "Password123@",
            ConfirmPassword = "Password123@"
        };

        var result = await authService.SendRegisterOtpAsync(model, "127.0.0.1");

        Assert.True(result.Succeeded);
        Assert.Single(emailService.SentOtps);
        Assert.Equal("vana@utc.edu.vn", emailService.SentOtps[0].To);
        Assert.Equal(AuthService.PurposeRegister, emailService.SentOtps[0].Purpose);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_ValidOtp_CreatesReaderAndLibraryCard_AndBurnsOtp()
    {
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);
        var emailService = new FakeEmailService();
        var rateLimiter = new FakeIpRateLimiterService();
        var logger = NullLogger<AuthService>.Instance;

        var readerType = new ReaderType
        {
            Id = Guid.NewGuid(),
            Code = "STUDENT",
            Name = "Sinh Viên",
            CardValidMonths = 48,
            MaxBorrowBooks = 5,
            LoanDays = 14,
            Status = EntityStatus.Active
        };
        context.ReaderTypes.Add(readerType);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, otpService, emailService, rateLimiter, timeProvider, logger);

        var model = new ReaderRegisterViewModel
        {
            FullName = "Tran Thi B",
            Username = "tranthib",
            Email = "thib@utc.edu.vn",
            Phone = "0912345678",
            ReaderTypeId = readerType.Id,
            Password = "Password123@",
            ConfirmPassword = "Password123@"
        };

        // 1. Generate OTP
        var sendResult = await authService.SendRegisterOtpAsync(model, "127.0.0.1");
        Assert.True(sendResult.Succeeded);
        var otp = emailService.SentOtps[0].Otp;

        // 2. Complete Registration
        var completeResult = await authService.CompleteRegistrationAsync(model, otp, "127.0.0.1");

        Assert.True(completeResult.Succeeded);
        Assert.NotNull(completeResult.Data);
        Assert.NotNull(completeResult.Data.CardNo);
        Assert.Contains("2026", completeResult.Data.CardNo);

        // Check Reader and LibraryCard in DB
        var savedReader = await context.Readers.FirstOrDefaultAsync(r => r.Username == "tranthib");
        Assert.NotNull(savedReader);
        Assert.Equal("Tran Thi B", savedReader.FullName);
        Assert.True(PasswordHasher.VerifyPassword("Password123@", savedReader.PasswordHash));

        Assert.Equal(completeResult.Data.CardNo, savedReader.CardNo);
        Assert.Equal(EntityStatus.Active, savedReader.CardStatus);

        // OTP must be burned
        var isOtpValidAfterBurn = await otpService.VerifyOtpAsync("thib@utc.edu.vn", otp, AuthService.PurposeRegister);
        Assert.False(isOtpValidAfterBurn.IsSuccess);
    }

    [Fact]
    public async Task InitiateReaderLoginAsync_ValidCredentials_Sends2FaOtp()
    {
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);
        var emailService = new FakeEmailService();
        var rateLimiter = new FakeIpRateLimiterService();
        var logger = NullLogger<AuthService>.Instance;

        var reader = new Reader
        {
            Id = Guid.NewGuid(),
            FullName = "Le Van C",
            Username = "levanc",
            Email = "vanc@utc.edu.vn",
            Phone = "0933333333",
            PasswordHash = PasswordHasher.HashPassword("CorrectPassword123!"),
            Status = EntityStatus.Active,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        context.Readers.Add(reader);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, otpService, emailService, rateLimiter, timeProvider, logger);

        var loginModel = new ReaderLoginViewModel
        {
            UsernameOrEmail = "levanc",
            Password = "CorrectPassword123!"
        };

        var result = await authService.InitiateReaderLoginAsync(loginModel, "127.0.0.1");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("vanc@utc.edu.vn", result.Data.Email);
        Assert.Equal(AuthService.PurposeReaderLogin, result.Data.Purpose);
        Assert.Single(emailService.SentOtps);
    }

    [Fact]
    public async Task InitiateReaderLoginAsync_FiveFailedAttempts_LocksAccountFor15Minutes()
    {
        using var context = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;
        var timeProvider = new TestDateTimeProvider(now);
        var otpService = new OtpService(context, timeProvider);
        var emailService = new FakeEmailService();
        var rateLimiter = new FakeIpRateLimiterService();
        var logger = NullLogger<AuthService>.Instance;

        var reader = new Reader
        {
            Id = Guid.NewGuid(),
            FullName = "Target Account",
            Username = "target_user",
            Email = "target@utc.edu.vn",
            Phone = "0944444444",
            PasswordHash = PasswordHasher.HashPassword("RealPassword123!"),
            Status = EntityStatus.Active
        };
        context.Readers.Add(reader);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, otpService, emailService, rateLimiter, timeProvider, logger);

        var badLoginModel = new ReaderLoginViewModel
        {
            UsernameOrEmail = "target_user",
            Password = "WrongPassword123!"
        };

        // 5 consecutive failed attempts
        for (int i = 1; i <= 5; i++)
        {
            var res = await authService.InitiateReaderLoginAsync(badLoginModel, "127.0.0.1");
            Assert.False(res.Succeeded);
        }

        // Account should now be locked
        var lockedReader = await context.Readers.FirstAsync(r => r.Username == "target_user");
        Assert.Equal(5, lockedReader.FailedLoginAttempts);
        Assert.NotNull(lockedReader.LockedUntil);
        Assert.True(lockedReader.LockedUntil.Value > now);

        // 6th attempt should be blocked due to account lockout
        var attempt6 = await authService.InitiateReaderLoginAsync(badLoginModel, "127.0.0.1");
        Assert.False(attempt6.Succeeded);
        Assert.Contains("bị tạm khóa", attempt6.Message);
    }

    [Fact]
    public async Task CompleteReaderLoginOtpAsync_ValidOtp_ReturnsClaimsPrincipalWithReaderRole()
    {
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);
        var emailService = new FakeEmailService();
        var rateLimiter = new FakeIpRateLimiterService();
        var logger = NullLogger<AuthService>.Instance;

        var reader = new Reader
        {
            Id = Guid.NewGuid(),
            FullName = "Pham Thi D",
            Username = "phamthid",
            Email = "thid@utc.edu.vn",
            Phone = "0955555555",
            PasswordHash = PasswordHasher.HashPassword("RealPass123!"),
            Status = EntityStatus.Active,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            FailedLoginAttempts = 2
        };
        context.Readers.Add(reader);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, otpService, emailService, rateLimiter, timeProvider, logger);

        // Generate OTP
        var otp = await otpService.GenerateOtpAsync("thid@utc.edu.vn", AuthService.PurposeReaderLogin);

        // Complete 2FA login
        var result = await authService.CompleteReaderLoginOtpAsync("thid@utc.edu.vn", otp, "127.0.0.1");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);

        var claims = result.Data.Claims;
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Reader");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == "phamthid");
        Assert.Contains(claims, c => c.Type == "FullName" && c.Value == "Pham Thi D");

        // Failed attempts reset to 0
        var reloaded = await context.Readers.FirstAsync(r => r.Username == "phamthid");
        Assert.Equal(0, reloaded.FailedLoginAttempts);
    }

    [Fact]
    public async Task InitiateStaffLoginAsync_ExistingStaff_SendsPasswordlessOtp()
    {
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);
        var emailService = new FakeEmailService();
        var rateLimiter = new FakeIpRateLimiterService();
        var logger = NullLogger<AuthService>.Instance;

        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            Username = "admin_qa",
            FullName = "Admin Quoc Anh",
            Email = "trantien7122005@gmail.com",
            Phone = "0966666666",
            Role = "Admin",
            Status = EntityStatus.Active,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        context.Staff.Add(staff);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, otpService, emailService, rateLimiter, timeProvider, logger);

        var staffModel = new StaffLoginViewModel { Email = "trantien7122005@gmail.com" };
        var result = await authService.InitiateStaffLoginAsync(staffModel, "127.0.0.1");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("trantien7122005@gmail.com", result.Data.Email);
        Assert.Equal(AuthService.PurposeStaffLogin, result.Data.Purpose);
        Assert.Single(emailService.SentOtps);
    }

    [Fact]
    public async Task InitiateStaffLoginAsync_NonExistingStaff_ReturnsGenericSuccess_WithoutLeakingAccount()
    {
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);
        var emailService = new FakeEmailService();
        var rateLimiter = new FakeIpRateLimiterService();
        var logger = NullLogger<AuthService>.Instance;

        var authService = new AuthService(context, otpService, emailService, rateLimiter, timeProvider, logger);

        var staffModel = new StaffLoginViewModel { Email = "ghost_staff@gmail.com" };
        var result = await authService.InitiateStaffLoginAsync(staffModel, "127.0.0.1");

        // SEC-09: Account Enumeration defense - returns success outwardly
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        // But NO email is sent to avoid mail toll fraud & disclosure
        Assert.Empty(emailService.SentOtps);
    }

    [Fact]
    public async Task CompleteStaffLoginOtpAsync_ValidOtp_ReturnsClaimsPrincipalWithStaffRole()
    {
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);
        var emailService = new FakeEmailService();
        var rateLimiter = new FakeIpRateLimiterService();
        var logger = NullLogger<AuthService>.Instance;

        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            Username = "librarian_01",
            FullName = "Thu Thu Nguyen",
            Email = "librarian@gmail.com",
            Phone = "0977777777",
            Role = "Librarian",
            Status = EntityStatus.Active,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        context.Staff.Add(staff);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, otpService, emailService, rateLimiter, timeProvider, logger);

        // Generate OTP
        var otp = await otpService.GenerateOtpAsync("librarian@gmail.com", AuthService.PurposeStaffLogin);

        // Complete staff login
        var result = await authService.CompleteStaffLoginOtpAsync("librarian@gmail.com", otp, "127.0.0.1");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);

        var claims = result.Data.Claims;
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Librarian");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == "librarian_01");
        Assert.Contains(claims, c => c.Type == "FullName" && c.Value == "Thu Thu Nguyen");
    }
}
