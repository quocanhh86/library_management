using Microsoft.EntityFrameworkCore;
using web_mvc_management.Data;
using web_mvc_management.Infrastructure;
using web_mvc_management.Repositories;

namespace web_mvc_management.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký DbContext với kết nối bảo mật (ưu tiên biến môi trường từ .env).
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
        // 1. Kiểm tra ConnectionStrings:DefaultConnection từ Configuration (.env hoặc appsettings)
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // 2. Nếu chưa có hoặc là placeholder, thử ghép từ các biến môi trường rời rạc (DB_HOST, DB_USER...)
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("YOUR_PASSWORD"))
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
        return services;
    }
}
