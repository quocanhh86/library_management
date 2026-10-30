using System;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using web_mvc_management.Constant;
using web_mvc_management.Data;
using web_mvc_management.Infrastructure;
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
    /// Trích xuất chuỗi kết nối an toàn từ IConfiguration hoặc biến môi trường .env.
    /// </summary>
    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var host = configuration["DB_HOST"] ?? Environment.GetEnvironmentVariable("DB_HOST");
            var port = configuration["DB_PORT"] ?? Environment.GetEnvironmentVariable("DB_PORT") ?? "1433";
            var db = configuration["DB_NAME"] ?? Environment.GetEnvironmentVariable("DB_NAME");
            var user = configuration["DB_USER"] ?? Environment.GetEnvironmentVariable("DB_USER");
            var pass = configuration["DB_PASSWORD"] ?? Environment.GetEnvironmentVariable("DB_PASSWORD");

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
                "Vui lòng tạo file .env từ .env.example và điền thông tin kết nối DB (ConnectionStrings__DefaultConnection hoặc DB_HOST, DB_NAME, DB_USER, DB_PASSWORD)."
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
            });

        return services;
    }

    /// <summary>
    /// Đăng ký các hạ tầng dùng chung (Infrastructure).
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

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
    /// Đăng ký các application services (sẽ mở rộng ở các module chức năng).
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IOtpService, OtpService>();
        return services;
    }
}
