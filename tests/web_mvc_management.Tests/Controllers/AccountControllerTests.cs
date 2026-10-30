using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Controllers;
using web_mvc_management.Data;
using web_mvc_management.Entity;
using web_mvc_management.Extensions;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Auth;
using web_mvc_management.Services;
using web_mvc_management.Tests.Services;
using web_mvc_management.Util;
using Xunit;

namespace web_mvc_management.Tests.Controllers;

public class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();
    public string Id { get; } = Guid.NewGuid().ToString();
    public bool IsAvailable => true;
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;
    public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
}

public class TestTempDataProvider : ITempDataProvider
{
    private readonly Dictionary<string, object> _data = new();
    public IDictionary<string, object> LoadTempData(HttpContext context) => _data;
    public void SaveTempData(HttpContext context, IDictionary<string, object> values)
    {
        _data.Clear();
        foreach (var kvp in values) _data[kvp.Key] = kvp.Value;
    }
}

public class AccountControllerTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private (AccountController Controller, TestSession Session, ApplicationDbContext DbContext, IAuthService AuthService)
        CreateController(ApplicationDbContext context)
    {
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var otpService = new OtpService(context, timeProvider);
        var emailService = new FakeEmailService();
        var rateLimiter = new FakeIpRateLimiterService();
        var logger = NullLogger<AuthService>.Instance;

        var authService = new AuthService(context, otpService, emailService, rateLimiter, timeProvider, logger);
        var controller = new AccountController(authService, rateLimiter, context);

        var session = new TestSession();
        var httpContext = new DefaultHttpContext();
        httpContext.Session = session;

        // Mock authentication service và MVC Routing
        var authServiceMock = new FakeAuthenticationService();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddRouting();
        serviceCollection.AddControllersWithViews();
        serviceCollection.AddSingleton<IAuthenticationService>(authServiceMock);
        httpContext.RequestServices = serviceCollection.BuildServiceProvider();

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.Url = new FakeUrlHelper();

        return (controller, session, context, authService);
    }

    [Fact]
    public async Task Register_Post_ValidModel_SavesToSession_AndRedirectsToVerifyOtp()
    {
        using var context = CreateInMemoryDbContext();
        var readerType = new ReaderType
        {
            Id = Guid.NewGuid(),
            Code = "STUDENT",
            Name = "Sinh Viên",
            Status = EntityStatus.Active
        };
        context.ReaderTypes.Add(readerType);
        await context.SaveChangesAsync();

        var (controller, session, _, _) = CreateController(context);

        var model = new ReaderRegisterViewModel
        {
            FullName = "Tran Van C",
            Username = "tranvanc",
            Email = "vanc@utc.edu.vn",
            Phone = "0987654321",
            ReaderTypeId = readerType.Id,
            Password = "Password123@",
            ConfirmPassword = "Password123@"
        };

        var result = await controller.Register(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AccountController.VerifyOtp), redirect.ActionName);
        Assert.Equal(AuthService.PurposeRegister, redirect.RouteValues?["purpose"]);

        // Kiểm tra dữ liệu được lưu vào Stateful Session
        var pendingReg = session.GetObject<ReaderRegisterViewModel>("PendingRegistration");
        Assert.NotNull(pendingReg);
        Assert.Equal("tranvanc", pendingReg.Username);
        Assert.Equal("vanc@utc.edu.vn", pendingReg.Email);
    }

    [Fact]
    public async Task Login_Post_ValidCredentials_SavesEmailToSession_AndRedirectsToVerifyOtp()
    {
        using var context = CreateInMemoryDbContext();
        var reader = new Reader
        {
            Id = Guid.NewGuid(),
            FullName = "Nguyen Reader",
            Username = "reader01",
            Email = "reader01@utc.edu.vn",
            Phone = "0911111111",
            PasswordHash = PasswordHasher.HashPassword("SecretPassword123!"),
            Status = EntityStatus.Active,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        context.Readers.Add(reader);
        await context.SaveChangesAsync();

        var (controller, session, _, _) = CreateController(context);

        var model = new ReaderLoginViewModel
        {
            UsernameOrEmail = "reader01",
            Password = "SecretPassword123!"
        };

        var result = await controller.Login(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AccountController.VerifyOtp), redirect.ActionName);
        Assert.Equal(AuthService.PurposeReaderLogin, redirect.RouteValues?["purpose"]);

        // Kiểm tra session lưu trữ email đang chờ xác thực 2FA
        Assert.Equal("reader01@utc.edu.vn", session.GetString("PendingLoginEmail"));
        Assert.Equal(AuthService.PurposeReaderLogin, session.GetString("PendingOtpPurpose"));
    }

    [Fact]
    public async Task StaffLogin_Post_ValidStaff_SavesEmailToSession_AndRedirectsToVerifyOtp()
    {
        using var context = CreateInMemoryDbContext();
        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            Username = "staff_utc",
            FullName = "Can Bo Thuan",
            Email = "canbo@utc.edu.vn",
            Phone = "0922222222",
            Role = AppRoles.Librarian,
            Status = EntityStatus.Active,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        context.Staff.Add(staff);
        await context.SaveChangesAsync();

        var (controller, session, _, _) = CreateController(context);

        var model = new StaffLoginViewModel { Email = "canbo@utc.edu.vn" };
        var result = await controller.StaffLogin(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AccountController.VerifyOtp), redirect.ActionName);
        Assert.Equal(AuthService.PurposeStaffLogin, redirect.RouteValues?["purpose"]);

        Assert.Equal("canbo@utc.edu.vn", session.GetString("PendingLoginEmail"));
        Assert.Equal(AuthService.PurposeStaffLogin, session.GetString("PendingOtpPurpose"));
    }

    [Fact]
    public void VerifyOtp_Get_WithoutPendingSession_RedirectsBackWithErrorMessage()
    {
        using var context = CreateInMemoryDbContext();
        var (controller, _, _, _) = CreateController(context);

        var result = controller.VerifyOtp(AuthService.PurposeRegister);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AccountController.Register), redirect.ActionName);
        Assert.True(controller.TempData.ContainsKey("ErrorMessage"));
    }

    [Fact]
    public async Task VerifyOtp_Post_RegisterPurpose_ValidOtp_CompletesAndClearsSession()
    {
        using var context = CreateInMemoryDbContext();
        var readerType = new ReaderType
        {
            Id = Guid.NewGuid(),
            Code = "STUDENT",
            Name = "Sinh Viên",
            Status = EntityStatus.Active
        };
        context.ReaderTypes.Add(readerType);
        await context.SaveChangesAsync();

        var (controller, session, _, _) = CreateController(context);

        var pendingReg = new ReaderRegisterViewModel
        {
            FullName = "Hoang Van D",
            Username = "hoangvand",
            Email = "vand@utc.edu.vn",
            Phone = "0944444444",
            ReaderTypeId = readerType.Id,
            Password = "Password123@",
            ConfirmPassword = "Password123@"
        };

        // Giả lập trạng thái Session sau khi form Register đã gửi OTP
        session.SetObject("PendingRegistration", pendingReg);
        session.SetString("PendingOtpPurpose", AuthService.PurposeRegister);

        // Sinh OTP hợp lệ
        var otpService = new OtpService(context, new TestDateTimeProvider(DateTimeOffset.UtcNow));
        var otp = await otpService.GenerateOtpAsync("vand@utc.edu.vn", AuthService.PurposeRegister);

        var verifyModel = new VerifyOtpViewModel
        {
            Email = "vand@utc.edu.vn",
            OtpCode = otp,
            Purpose = AuthService.PurposeRegister
        };

        var result = await controller.VerifyOtp(verifyModel);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AccountController.Login), redirect.ActionName);
        Assert.True(controller.TempData.ContainsKey("SuccessMessage"));

        // Session đăng ký phải được dọn sạch
        Assert.Null(session.GetObject<ReaderRegisterViewModel>("PendingRegistration"));
    }

    [Fact]
    public async Task VerifyOtp_Post_LoginPurpose_ValidOtp_IssuesCookieAndClearsSession()
    {
        using var context = CreateInMemoryDbContext();
        var reader = new Reader
        {
            Id = Guid.NewGuid(),
            FullName = "Do Thi E",
            Username = "dothie",
            Email = "thie@utc.edu.vn",
            Phone = "0955555555",
            PasswordHash = PasswordHasher.HashPassword("Password123@"),
            Status = EntityStatus.Active,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        context.Readers.Add(reader);
        await context.SaveChangesAsync();

        var (controller, session, _, _) = CreateController(context);

        // Giả lập trạng thái Session sau khi kiểm tra mật khẩu hợp lệ
        session.SetString("PendingLoginEmail", "thie@utc.edu.vn");
        session.SetString("PendingOtpPurpose", AuthService.PurposeReaderLogin);

        var otpService = new OtpService(context, new TestDateTimeProvider(DateTimeOffset.UtcNow));
        var otp = await otpService.GenerateOtpAsync("thie@utc.edu.vn", AuthService.PurposeReaderLogin);

        var verifyModel = new VerifyOtpViewModel
        {
            Email = "thie@utc.edu.vn",
            OtpCode = otp,
            Purpose = AuthService.PurposeReaderLogin
        };

        var result = await controller.VerifyOtp(verifyModel);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        Assert.True(controller.TempData.ContainsKey("SuccessMessage"));

        // Session đăng nhập phải được dọn sạch
        Assert.Null(session.GetString("PendingLoginEmail"));
    }
}

public class FakeAuthenticationService : IAuthenticationService
{
    public ClaimsPrincipal? SignedInPrincipal { get; private set; }

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
        Task.FromResult(AuthenticateResult.NoResult());

    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
    {
        SignedInPrincipal = principal;
        return Task.CompletedTask;
    }

    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        SignedInPrincipal = null;
        return Task.CompletedTask;
    }
}

public class FakeUrlHelper : IUrlHelper
{
    public ActionContext ActionContext { get; set; } = new();
    public string? Action(Microsoft.AspNetCore.Mvc.Routing.UrlActionContext actionContext) => $"/{actionContext.Action}";
    public string? Content(string? contentPath) => contentPath;
    public bool IsLocalUrl(string? url) => true;
    public string? Link(string? routeName, object? values) => "/";
    public string? RouteUrl(Microsoft.AspNetCore.Mvc.Routing.UrlRouteContext routeContext) => "/";
}
