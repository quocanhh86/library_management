using Microsoft.EntityFrameworkCore;
using web_mvc_management.Data;
using web_mvc_management.Infrastructure;
using web_mvc_management.Repositories;

namespace web_mvc_management.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký DbContext và các service liên quan database.
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)
            )
        );

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
        return services;
    }
}
