using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Data;
using web_mvc_management.Entity;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Auth;
using web_mvc_management.Services;
using web_mvc_management.Tests.Controllers;
using web_mvc_management.Tests.Services;
using web_mvc_management.Util;
using Xunit;

namespace web_mvc_management.Tests.Security;

/// <summary>
/// Bộ kiểm thử chuyên sâu đối chiếu tiêu chuẩn bảo mật quốc tế:
/// - OWASP ASVS v4.0 (Application Security Verification Standard)
/// - NIST SP 800-63B (Digital Identity Guidelines)
/// @blast-radius: core, security
/// </summary>
public class OwaspNistSecurityTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    // =========================================================================
    // 1. OWASP ASVS 2.1 & 2.2 / NIST §5.1 & §5.2: PASSWORD & BRUTE-FORCE DEFENSE
    // =========================================================================

    [Fact]
    public async Task Sec09_Asvs217_ReaderLogin_NonExistentUser_ReturnsIdenticalGenericMessage()
    {
        // OWASP ASVS 2.1.7: Chống dò quét tài khoản (Account Enumeration Defense)
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);
        var emailService = new FakeEmailService();
        var rateLimiter = new FakeIpRateLimiterService();
        var logger = NullLogger<AuthService>.Instance;

        var existingReader = new Reader
        {
            Id = Guid.NewGuid(),
            FullName = "Existing User",
            Username = "existing_user",
            Email = "existing@utc.edu.vn",
            PasswordHash = PasswordHasher.HashPassword("RealPass123!"),
            Status = EntityStatus.Active
        };
        context.Readers.Add(existingReader);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, otpService, emailService, rateLimiter, timeProvider, logger);

        // Trường hợp 1: Tài khoản hoàn toàn không tồn tại
        var nonExistentResult = await authService.InitiateReaderLoginAsync(
            new ReaderLoginViewModel { UsernameOrEmail = "ghost_user", Password = "AnyPassword123!" }, "127.0.0.1");

        // Trường hợp 2: Tài khoản có thật nhưng nhập sai mật khẩu
        var wrongPassResult = await authService.InitiateReaderLoginAsync(
            new ReaderLoginViewModel { UsernameOrEmail = "existing_user", Password = "WrongPassword123!" }, "127.0.0.1");

        Assert.False(nonExistentResult.Succeeded);
        Assert.False(wrongPassResult.Succeeded);
        // Thông báo lỗi phải đồng nhất, không tiết lộ tài khoản có tồn tại hay không
        Assert.Contains("không chính xác", nonExistentResult.Message);
        Assert.Contains("không chính xác", wrongPassResult.Message);
    }

    [Fact]
    public void Sec15_Cwe208_DummyPasswordHash_ExecutesConsistentVerificationWithoutThrowing()
    {
        // SEC-15 / CWE-208: Timing Attack Defense - Hàm băm giả định chạy cùng thuật toán PBKDF2
        var exception = Record.Exception(() =>
        {
            var result = PasswordHasher.VerifyDummyPassword("AnyAttackerSuppliedPassword123!");
            Assert.False(result); // Luôn trả về false
        });

        Assert.Null(exception);
    }

    [Fact]
    public async Task Sec09_Asvs217_StaffLogin_NonExistentEmail_ReturnsGenericSuccess_WithoutSendingMail()
    {
        // OWASP ASVS 2.1.7: Passwordless login không được làm rò rỉ email cán bộ
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);
        var emailService = new FakeEmailService();
        var rateLimiter = new FakeIpRateLimiterService();
        var logger = NullLogger<AuthService>.Instance;

        var authService = new AuthService(context, otpService, emailService, rateLimiter, timeProvider, logger);

        var result = await authService.InitiateStaffLoginAsync(
            new StaffLoginViewModel { Email = "unknown_staff@utc.edu.vn" }, "127.0.0.1");

        // Bên ngoài nhận phản hồi như bình thường để không lộ thông tin
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.RequiresOtp);

        // Nhưng bên trong KHÔNG phát tán email (chống Mail Toll Fraud)
        Assert.Empty(emailService.SentOtps);
    }

    [Fact]
    public async Task Sec11_Nist522_Asvs221_ReaderLogin_FiveConsecutiveFailedAttempts_LocksAccountFor15Minutes()
    {
        // NIST SP 800-63B §5.2.2: Tạm khóa 15 phút sau 5 lần thất bại liên tiếp
        using var context = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;
        var timeProvider = new TestDateTimeProvider(now);
        var otpService = new OtpService(context, timeProvider);
        var emailService = new FakeEmailService();
        var rateLimiter = new FakeIpRateLimiterService();
        var logger = NullLogger<AuthService>.Instance;

        var originalStamp = Guid.NewGuid().ToString("N");
        var reader = new Reader
        {
            Id = Guid.NewGuid(),
            FullName = "Brute Force Target",
            Username = "victim_account",
            Email = "victim@utc.edu.vn",
            PasswordHash = PasswordHasher.HashPassword("SuperSecret123!"),
            Status = EntityStatus.Active,
            SecurityStamp = originalStamp,
            FailedLoginAttempts = 0
        };
        context.Readers.Add(reader);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, otpService, emailService, rateLimiter, timeProvider, logger);
        var loginModel = new ReaderLoginViewModel { UsernameOrEmail = "victim_account", Password = "BadPassword!" };

        // 5 lần thử sai liên tiếp
        for (int i = 1; i <= 5; i++)
        {
            var res = await authService.InitiateReaderLoginAsync(loginModel, "127.0.0.1");
            Assert.False(res.Succeeded);
        }

        var lockedReader = await context.Readers.FirstAsync(r => r.Username == "victim_account");
        Assert.Equal(5, lockedReader.FailedLoginAttempts);
        Assert.NotNull(lockedReader.LockedUntil);
        Assert.True(lockedReader.LockedUntil.Value > now);
        Assert.Equal(now.AddMinutes(15), lockedReader.LockedUntil.Value);

        // SecurityStamp phải được thay đổi để thu hồi mọi phiên đang đăng nhập (SEC-05, SEC-21)
        Assert.NotEqual(originalStamp, lockedReader.SecurityStamp);

        // Lần thứ 6 dù nhập đúng mật khẩu thật cũng phải bị từ chối do đang trong thời gian khóa
        var correctModel = new ReaderLoginViewModel { UsernameOrEmail = "victim_account", Password = "SuperSecret123!" };
        var blockedRes = await authService.InitiateReaderLoginAsync(correctModel, "127.0.0.1");
        Assert.False(blockedRes.Succeeded);
        Assert.Contains("tạm khóa", blockedRes.Message);

        // Sau 16 phút (hết hạn khóa), người dùng nhập đúng mật khẩu thì được mở khóa
        timeProvider.UtcNow = now.AddMinutes(16);
        var successAfterExpiry = await authService.InitiateReaderLoginAsync(correctModel, "127.0.0.1");
        Assert.True(successAfterExpiry.Succeeded);
        Assert.NotNull(successAfterExpiry.Data);
    }

    // =========================================================================
    // 2. OWASP ASVS 2.8 / NIST §5.1.3 & §5.1.4: OTP BURNING & DOMAIN ISOLATION
    // =========================================================================

    [Fact]
    public async Task Sec04_Asvs285_Nist5142_OtpBurningEngine_BurnsOtpImmediately_AfterFiveFailedAttempts()
    {
        // OWASP ASVS 2.8.5: Tiêu hủy mã OTP sau 5 lần nhập sai
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);

        var email = "otp_target@utc.edu.vn";
        var realOtp = await otpService.GenerateOtpAsync(email, AuthService.PurposeReaderLogin);

        // Thử sai 4 lần -> mã vẫn còn hiệu lực
        for (int i = 1; i <= 4; i++)
        {
            var res = await otpService.VerifyOtpAsync(email, AuthService.PurposeReaderLogin, "000000");
            Assert.False(res.IsSuccess);
            Assert.False(res.IsBurned);
            Assert.Equal(5 - i, res.RemainingAttempts);
        }

        // Lần thứ 5 sai -> Mã bị thiêu hủy hoàn toàn (Burned)
        var fifthFail = await otpService.VerifyOtpAsync(email, AuthService.PurposeReaderLogin, "000000");
        Assert.False(fifthFail.IsSuccess);
        Assert.True(fifthFail.IsBurned);
        Assert.Contains("tiêu hủy", fifthFail.Message);

        // Lần thứ 6 dù nhập ĐÚNG mã thật ban đầu cũng PHẢI bị từ chối
        var sixthTryWithRealOtp = await otpService.VerifyOtpAsync(email, AuthService.PurposeReaderLogin, realOtp);
        Assert.False(sixthTryWithRealOtp.IsSuccess);
        Assert.Contains("không tồn tại hoặc đã được sử dụng", sixthTryWithRealOtp.Message);
    }

    [Fact]
    public async Task Sec12_Nist513_Asvs286_OtpPurposeIsolation_RejectsCrossDomainUsage()
    {
        // NIST SP 800-63B §5.1.3 & SEC-12: Phân lập tuyệt đối không gian mục đích OTP
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);

        var email = "student_cross@utc.edu.vn";

        // Sinh mã OTP cho mục đích Đăng ký mở thẻ (REGISTER)
        var registerOtp = await otpService.GenerateOtpAsync(email, AuthService.PurposeRegister);

        // Thử dùng mã REGISTER để xác thực Đăng nhập Độc giả (READER_LOGIN) -> Phải thất bại!
        var readerLoginAttempt = await otpService.VerifyOtpAsync(email, AuthService.PurposeReaderLogin, registerOtp);
        Assert.False(readerLoginAttempt.IsSuccess);

        // Thử dùng mã REGISTER để xác thực Cán bộ (STAFF_LOGIN) -> Phải thất bại!
        var staffLoginAttempt = await otpService.VerifyOtpAsync(email, AuthService.PurposeStaffLogin, registerOtp);
        Assert.False(staffLoginAttempt.IsSuccess);

        // Dùng đúng mục đích REGISTER -> Thành công
        var validAttempt = await otpService.VerifyOtpAsync(email, AuthService.PurposeRegister, registerOtp);
        Assert.True(validAttempt.IsSuccess);
    }

    [Fact]
    public async Task Sec14_Asvs284_OtpSingleUse_CannotBeReplayed()
    {
        // OWASP ASVS 2.8.4: Chống Replay Attack - OTP chỉ được sử dụng đúng 1 lần
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);

        var email = "replay_target@utc.edu.vn";
        var otp = await otpService.GenerateOtpAsync(email, AuthService.PurposeStaffLogin);

        // Lần 1: Xác thực thành công
        var firstUse = await otpService.VerifyOtpAsync(email, AuthService.PurposeStaffLogin, otp);
        Assert.True(firstUse.IsSuccess);

        // Lần 2: Tấn công phát lại (Replay Attack) -> Phải thất bại
        var secondUse = await otpService.VerifyOtpAsync(email, AuthService.PurposeStaffLogin, otp);
        Assert.False(secondUse.IsSuccess);
        Assert.Equal(OtpStatus.NotFound, secondUse.Status);
    }

    [Fact]
    public async Task Asvs282_OtpExpiration_RejectsCodeAfterFiveMinutes()
    {
        // OWASP ASVS 2.8.2: Hết hạn sau 5 phút (300 giây)
        using var context = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;
        var timeProvider = new TestDateTimeProvider(now);
        var otpService = new OtpService(context, timeProvider);

        var email = "expiry_test@utc.edu.vn";
        var otp = await otpService.GenerateOtpAsync(email, AuthService.PurposeRegister);

        // Thời gian trôi qua 5 phút 1 giây
        timeProvider.UtcNow = now.AddMinutes(5).AddSeconds(1);

        var verifyResult = await otpService.VerifyOtpAsync(email, AuthService.PurposeRegister, otp);
        Assert.False(verifyResult.IsSuccess);
        Assert.Equal(OtpStatus.Expired, verifyResult.Status);
    }

    [Fact]
    public async Task Sec14_OtpConcurrency_ParallelVerifications_OnlyOneSucceeds()
    {
        // SEC-14: Concurrency Race Condition Defense
        // 10 luồng cùng lúc xác thực 1 mã OTP -> Duy nhất 1 luồng thành công
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var email = "concurrency@utc.edu.vn";
        string otp;

        using (var setupContext = new ApplicationDbContext(options))
        {
            var otpService = new OtpService(setupContext, timeProvider);
            otp = await otpService.GenerateOtpAsync(email, AuthService.PurposeReaderLogin);
        }

        var tasks = new List<Task<OtpVerificationResult>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                // Mỗi luồng sử dụng một DbContext riêng trỏ tới cùng CSDL
                using var threadContext = new ApplicationDbContext(options);
                var svc = new OtpService(threadContext, timeProvider);
                return await svc.VerifyOtpAsync(email, AuthService.PurposeReaderLogin, otp);
            }));
        }

        var results = await Task.WhenAll(tasks);
        var successCount = 0;
        foreach (var r in results)
        {
            if (r.IsSuccess) successCount++;
        }

        Assert.Equal(1, successCount);
    }

    // =========================================================================
    // 3. NIST SP 800-63B §7.1 / OWASP ASVS 3.4.3: SESSION INTEGRITY & REVOCATION
    // =========================================================================

    [Fact]
    public async Task Sec05_Sec21_Nist71_SecurityStampValidator_RejectsSession_WhenStampChanges()
    {
        // NIST SP 800-63B §7.1: Thu hồi phiên đăng nhập khi SecurityStamp thay đổi (đổi mật khẩu/reset)
        using var context = CreateInMemoryDbContext();
        var readerId = Guid.NewGuid();
        var initialStamp = Guid.NewGuid().ToString("N");

        var reader = new Reader
        {
            Id = readerId,
            Username = "reader_session",
            Email = "session@utc.edu.vn",
            Status = EntityStatus.Active,
            SecurityStamp = initialStamp
        };
        context.Readers.Add(reader);
        await context.SaveChangesAsync();

        // 1. Tạo CookieValidatePrincipalContext với stamp cũ
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, readerId.ToString()),
            new(ClaimTypes.Role, AppRoles.Reader),
            new(SecurityStampValidator.SecurityStampClaimType, initialStamp)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddSingleton(context);
        var authServiceMock = new FakeAuthenticationService();
        services.AddSingleton<IAuthenticationService>(authServiceMock);
        httpContext.RequestServices = services.BuildServiceProvider();

        var authScheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        var authOptions = new CookieAuthenticationOptions();
        var authProperties = new AuthenticationProperties();

        var validateContext = new CookieValidatePrincipalContext(
            httpContext,
            authScheme,
            authOptions,
            new AuthenticationTicket(principal, authProperties, CookieAuthenticationDefaults.AuthenticationScheme));

        // Kiểm tra lần 1: Stamp trùng khớp -> Phiên hợp lệ
        await SecurityStampValidator.ValidatePrincipalAsync(validateContext);
        Assert.True(validateContext.ShouldRenew == false);

        // 2. Mô phỏng độc giả đổi mật khẩu -> SecurityStamp trong DB bị thay đổi
        reader.SecurityStamp = Guid.NewGuid().ToString("N");
        await context.SaveChangesAsync();

        // Kiểm tra lần 2: Stamp không khớp -> Phiên bị thu hồi ngay lập tức (Sign Out)
        await SecurityStampValidator.ValidatePrincipalAsync(validateContext);
        Assert.Null(validateContext.Principal); // Principal bị Reject
    }

    [Fact]
    public async Task Sec05_Sec21_Nist71_SecurityStampValidator_RejectsSession_WhenAccountIsLocked()
    {
        // NIST SP 800-63B §7.1: Thu hồi phiên của tài khoản đang bị tạm khóa (LockedUntil)
        using var context = CreateInMemoryDbContext();
        var readerId = Guid.NewGuid();
        var stamp = Guid.NewGuid().ToString("N");

        var reader = new Reader
        {
            Id = readerId,
            Username = "locked_user",
            Email = "locked@utc.edu.vn",
            Status = EntityStatus.Active,
            SecurityStamp = stamp,
            LockedUntil = DateTimeOffset.UtcNow.AddMinutes(15) // Tài khoản bị khóa
        };
        context.Readers.Add(reader);
        await context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, readerId.ToString()),
            new(ClaimTypes.Role, AppRoles.Reader),
            new(SecurityStampValidator.SecurityStampClaimType, stamp)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddSingleton(context);
        var authServiceMock = new FakeAuthenticationService();
        services.AddSingleton<IAuthenticationService>(authServiceMock);
        httpContext.RequestServices = services.BuildServiceProvider();

        var authScheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        var validateContext = new CookieValidatePrincipalContext(
            httpContext,
            authScheme,
            new CookieAuthenticationOptions(),
            new AuthenticationTicket(principal, new AuthenticationProperties(), CookieAuthenticationDefaults.AuthenticationScheme));

        await SecurityStampValidator.ValidatePrincipalAsync(validateContext);
        Assert.Null(validateContext.Principal); // Bị từ chối do tài khoản đang bị khóa
    }

    // =========================================================================
    // 4. SEC-17: ANTI-DOS, RATE LIMITING & COOLDOWN ENGINE
    // =========================================================================

    [Fact]
    public async Task Sec17_EmailCooldown_SendingOtpWithinSixtySeconds_IsBlocked()
    {
        // SEC-17: Khóa gửi lại OTP trong vòng 60 giây chống cạn kiệt tài nguyên
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["MailSettings:Host"] = "smtp.gmail.com",
            ["MailSettings:Port"] = "587",
            ["MailSettings:Username"] = "",
            ["MailSettings:Password"] = "",
            ["MailSettings:SenderDisplayName"] = "Thư Viện UTC"
        };
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var env = new MockHostEnvironment();
        var service = new EmailService(config, timeProvider, NullLogger<EmailService>.Instance, env);

        var email = $"cooldown_{Guid.NewGuid():N}@utc.edu.vn";

        // Lần 1: Gửi thành công
        var firstSend = await service.SendOtpEmailAsync(email, "123456", AuthService.PurposeRegister);
        Assert.True(firstSend.Succeeded);

        // Lần 2 (ngay lập tức): Bị chặn bởi Cooldown
        var secondSend = await service.SendOtpEmailAsync(email, "654321", AuthService.PurposeRegister);
        Assert.False(secondSend.Succeeded);
        Assert.Contains("vui lòng chờ", secondSend.Message.ToLower());

        // Kiểm tra thời gian còn lại
        var remaining = service.GetRemainingCooldownSeconds(email);
        Assert.True(remaining > 0 && remaining <= 60);
    }

    [Fact]
    public void Sec17_IpRateLimiter_SlidingWindow_BlocksRequestsExceedingThreshold()
    {
        // SEC-17: Giới hạn tần suất trích xuất theo IP
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new IpRateLimiterService(timeProvider);
        var ip = "192.168.1.100";
        var action = "SEND_OTP";
        var maxLimit = 5;

        // 5 requests đầu tiên được phép
        for (int i = 0; i < maxLimit; i++)
        {
            Assert.True(limiter.IsAllowed(ip, action, maxLimit));
        }

        // Request thứ 6 vượt ngưỡng -> Bị chặn
        Assert.False(limiter.IsAllowed(ip, action, maxLimit));

        // IP khác vẫn được phép
        Assert.True(limiter.IsAllowed("192.168.1.101", action, maxLimit));
    }

    [Fact]
    public void Sec17_IpRateLimiter_ExtractsRealIpFromCloudflareAndForwardedHeaders()
    {
        // SEC-17: Trích xuất IP thật an toàn phía sau Nginx/Cloudflare
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new IpRateLimiterService(timeProvider);
        var httpContext = new DefaultHttpContext();

        // 1. Nếu có header CF-Connecting-IP (Cloudflare)
        httpContext.Request.Headers["CF-Connecting-IP"] = "203.0.113.195";
        httpContext.Request.Headers["X-Forwarded-For"] = "10.0.0.1";
        Assert.Equal("203.0.113.195", limiter.GetClientIp(httpContext));

        // 2. Nếu chỉ có X-Forwarded-For
        httpContext.Request.Headers.Remove("CF-Connecting-IP");
        httpContext.Request.Headers["X-Forwarded-For"] = "198.51.100.42, 10.0.0.2";
        Assert.Equal("198.51.100.42", limiter.GetClientIp(httpContext));
    }

    // =========================================================================
    // 5. OWASP ASVS 2.1.1 & 13.1 / SEC-18: STRICT VALIDATION & INPUT HARDENING
    // =========================================================================

    [Theory]
    [InlineData("short1!", false)] // Ngắn hơn 8 ký tự
    [InlineData("lowercaseonly123@", false)] // Thiếu chữ hoa
    [InlineData("UPPERCASEONLY123@", false)] // Thiếu chữ thường
    [InlineData("NoSpecialChars123", false)] // Thiếu ký tự đặc biệt
    [InlineData("NoNumbersHere!@#$", false)] // Thiếu chữ số
    [InlineData("ValidStrongPassword123@", true)] // Đạt chuẩn ASVS 2.1.1
    public void Sec18_Asvs211_PasswordPolicy_EnforcesComplexityRules(string password, bool expectedValid)
    {
        var model = new ReaderRegisterViewModel
        {
            FullName = "Nguyen Test",
            Username = "test_user",
            Email = "test@utc.edu.vn",
            Phone = "0912345678",
            ReaderTypeId = Guid.NewGuid(),
            Password = password,
            ConfirmPassword = password
        };

        var context = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, context, validationResults, true);

        Assert.Equal(expectedValid, isValid);
    }

    [Theory]
    [InlineData("0912345678", true)] // Viettel/Vina hợp lệ
    [InlineData("0381234567", true)] // Viettel 038 hợp lệ
    [InlineData("0701234567", true)] // MobiFone 070 hợp lệ
    [InlineData("0123456789", false)] // Đầu 01 cũ 11 số (đã bãi bỏ)
    [InlineData("091234567", false)] // Thiếu 1 số (9 số)
    [InlineData("09123456789", false)] // Thừa 1 số (11 số)
    [InlineData("091234567a", false)] // Chứa chữ cái
    public void Sec18_VietnamesePhoneFormat_ValidatesMobileNetworkRules(string phone, bool expectedValid)
    {
        var model = new ReaderRegisterViewModel
        {
            FullName = "Nguyen Test",
            Username = "test_user",
            Email = "test@utc.edu.vn",
            Phone = phone,
            ReaderTypeId = Guid.NewGuid(),
            Password = "Password123@",
            ConfirmPassword = "Password123@"
        };

        var context = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, context, validationResults, true);

        Assert.Equal(expectedValid, isValid);
    }

    // =========================================================================
    // 6. SEC-22: DATABASE UNIQUE CONSTRAINTS & DUPLICATION DEFENSE
    // =========================================================================

    [Fact]
    public async Task Sec22_DuplicateRegistration_UsernameOrEmailOrPhone_ReturnsValidationErrors()
    {
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);
        var emailService = new FakeEmailService();
        var rateLimiter = new FakeIpRateLimiterService();
        var logger = NullLogger<AuthService>.Instance;

        var readerType = new ReaderType { Id = Guid.NewGuid(), Code = "STUDENT", Name = "Sinh Viên", Status = EntityStatus.Active };
        context.ReaderTypes.Add(readerType);

        var existing = new Reader
        {
            Id = Guid.NewGuid(),
            FullName = "Nguoi Dung Cu",
            Username = "existing_user",
            Email = "existing@utc.edu.vn",
            Phone = "0988888888",
            PasswordHash = PasswordHasher.HashPassword("Pass123@"),
            Status = EntityStatus.Active
        };
        context.Readers.Add(existing);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, otpService, emailService, rateLimiter, timeProvider, logger);

        // 1. Trùng Username
        var dupUser = await authService.SendRegisterOtpAsync(new ReaderRegisterViewModel
        {
            FullName = "Nguyen A",
            Username = "existing_user",
            Email = "new1@utc.edu.vn",
            Phone = "0911111111",
            ReaderTypeId = readerType.Id,
            Password = "Password123@",
            ConfirmPassword = "Password123@"
        }, "127.0.0.1");
        Assert.False(dupUser.Succeeded);
        Assert.Contains("Tên đăng nhập này đã được sử dụng", dupUser.Message);

        // 2. Trùng Email
        var dupEmail = await authService.SendRegisterOtpAsync(new ReaderRegisterViewModel
        {
            FullName = "Nguyen B",
            Username = "brand_new_user",
            Email = "existing@utc.edu.vn",
            Phone = "0922222222",
            ReaderTypeId = readerType.Id,
            Password = "Password123@",
            ConfirmPassword = "Password123@"
        }, "127.0.0.1");
        Assert.False(dupEmail.Succeeded);
        Assert.Contains("Địa chỉ email này đã được đăng ký", dupEmail.Message);

        // 3. Trùng SĐT
        var dupPhone = await authService.SendRegisterOtpAsync(new ReaderRegisterViewModel
        {
            FullName = "Nguyen C",
            Username = "brand_new_user_2",
            Email = "new2@utc.edu.vn",
            Phone = "0988888888",
            ReaderTypeId = readerType.Id,
            Password = "Password123@",
            ConfirmPassword = "Password123@"
        }, "127.0.0.1");
        Assert.False(dupPhone.Succeeded);
        Assert.Contains("Số điện thoại này đã được đăng ký", dupPhone.Message);
    }

    // =========================================================================
    // 7. SEC-01: CROSS-ROLE TAMPERING & DOMAIN CONFUSION DEFENSE
    // =========================================================================

    [Fact]
    public async Task Sec01_CrossRoleTampering_ReaderCannotAuthenticateAsStaff()
    {
        // SEC-01: Độc giả không thể sử dụng endpoint xác thực của Cán bộ thư viện
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);
        var emailService = new FakeEmailService();
        var rateLimiter = new FakeIpRateLimiterService();
        var logger = NullLogger<AuthService>.Instance;

        var reader = new Reader
        {
            Id = Guid.NewGuid(),
            FullName = "Sinh Vien Thuong",
            Username = "reader_student",
            Email = "student@utc.edu.vn",
            PasswordHash = PasswordHasher.HashPassword("Pass123@"),
            Status = EntityStatus.Active
        };
        context.Readers.Add(reader);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, otpService, emailService, rateLimiter, timeProvider, logger);

        // Thử xác thực hoàn tất đăng nhập Cán bộ bằng email của độc giả
        var result = await authService.CompleteStaffLoginOtpAsync("student@utc.edu.vn", "123456", "127.0.0.1");

        Assert.False(result.Succeeded);
        Assert.Contains("Không tìm thấy thông tin cán bộ", result.Message);
    }

    [Fact]
    public async Task Asvs331_Logout_Post_ClearsAllSessionState_AndSignsOutCookieScheme()
    {
        // OWASP ASVS 3.3.1: Hủy bỏ toàn bộ phiên làm việc khi đăng xuất
        using var context = CreateInMemoryDbContext();
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);
        var emailService = new FakeEmailService();
        var rateLimiter = new FakeIpRateLimiterService();
        var authService = new AuthService(context, otpService, emailService, rateLimiter, timeProvider, NullLogger<AuthService>.Instance);

        var controller = new web_mvc_management.Controllers.AccountController(authService, rateLimiter, context);
        var session = new TestSession();
        session.SetString("PendingLoginEmail", "test@utc.edu.vn");
        session.SetString("SomeRandomKey", "SecretValue");

        var httpContext = new DefaultHttpContext();
        httpContext.Session = session;
        var authServiceMock = new FakeAuthenticationService();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddControllersWithViews();
        services.AddSingleton<IAuthenticationService>(authServiceMock);
        httpContext.RequestServices = services.BuildServiceProvider();

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.Url = new FakeUrlHelper();

        var result = await controller.Logout();

        var redirect = Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);

        // Session phải bị dọn sạch hoàn toàn
        Assert.Empty(session.Keys);
        // AuthenticationService phải được gọi SignOut
        Assert.Null(authServiceMock.SignedInPrincipal);
    }
}
