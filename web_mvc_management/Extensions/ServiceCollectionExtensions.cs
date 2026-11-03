using System;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using web_mvc_management.Constant;
using web_mvc_management.Data;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models;
using web_mvc_management.Repositories;
using web_mvc_management.Services;

namespace web_mvc_management.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký DbContext với kết nối bảo mật.
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = ResolveConnectionString(configuration);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)
            )
        );

        return services;
    }

    /// <summary>
    /// Trích xuất chuỗi kết nối an toàn từ IConfiguration theo chuẩn phân cấp ASP.NET Core (ConnectionStrings / DatabaseSettings).
    /// </summary>
    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var host = configuration["DatabaseSettings:Host"]
                ?? Environment.GetEnvironmentVariable("DatabaseSettings__Host");

            var port = configuration["DatabaseSettings:Port"]
                ?? Environment.GetEnvironmentVariable("DatabaseSettings__Port")
                ?? "1433";

            var db = configuration["DatabaseSettings:Database"]
                ?? Environment.GetEnvironmentVariable("DatabaseSettings__Database");

            var user = configuration["DatabaseSettings:Username"]
                ?? Environment.GetEnvironmentVariable("DatabaseSettings__Username");

            var pass = configuration["DatabaseSettings:Password"]
                ?? Environment.GetEnvironmentVariable("DatabaseSettings__Password");

            if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(db))
            {
                var authPart = string.IsNullOrWhiteSpace(user)
                    ? "Trusted_Connection=True;"
                    : $"User Id={user};Password={pass};";

                connectionString = $"Server={host},{port};Database={db};{authPart}TrustServerCertificate=True;";
            }
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Không tìm thấy chuỗi kết nối Database hợp lệ. " +
                "Vui lòng tạo file .env từ .env.example và điền thông tin kết nối DB (ConnectionStrings__DefaultConnection hoặc DatabaseSettings__Host, DatabaseSettings__Database, DatabaseSettings__Username, DatabaseSettings__Password)."
            );
        }

        return connectionString;
    }

    /// <summary>
    /// Đăng ký Cookie Authentication với bảo mật cao (HttpOnly, Secure, SameSite, Sliding Expiration).
    /// </summary>
    public static IServiceCollection AddCookieAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = AppConstants.Auth.CookieScheme;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.LoginPath = AppConstants.Auth.LoginPath;
                options.LogoutPath = AppConstants.Auth.LogoutPath;
                options.AccessDeniedPath = AppConstants.Auth.AccessDeniedPath;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;

                // SEC-05, SEC-21: Kiểm tra tính toàn vẹn của phiên qua Security Stamp
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync
                };
            });

        return services;
    }

    /// <summary>
    /// Đăng ký các hạ tầng dùng chung (Infrastructure, Stateful Session, Caching).
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // Cấu hình Distributed Memory Cache & Stateful Session (MVC Server-Side State)
        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.Name = ".LibraryManagement.Session";
        });

        return services;
    }

    /// <summary>
    /// Đăng ký Base Repository và UnitOfWork.
    /// </summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    /// <summary>
    /// Đăng ký các application services và cấu hình IOptions.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        if (configuration != null)
        {
            services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        }

        services.AddScoped<IOtpService, OtpService>();
        services.AddSingleton<IEmailService, EmailService>();
        services.AddSingleton<IIpRateLimiterService, IpRateLimiterService>();
        services.AddScoped<IAuthService, AuthService>();

        // Dữ liệu mock cho giai đoạn dựng UI, đổi sang bản EF Core khi nối DB
        services.AddScoped<IBookService, Services.Mock.MockBookService>();
        services.AddScoped<ILibraryInfoService, Services.Mock.MockLibraryInfoService>();
        services.AddScoped<IBorrowService, Services.Mock.MockBorrowService>();

        return services;
    }
}
