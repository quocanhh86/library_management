using Microsoft.EntityFrameworkCore;
using web_mvc_management.Data;

namespace web_mvc_management.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký DbContext và các service liên quan database.
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        _ = services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)
            )
        );

        return services;
    }

    /// <summary>
    /// Đăng ký các application services (Business Logic).
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // TODO: Đăng ký Services
        // services.AddScoped<IYourService, YourService>();

        return services;
    }

    /// <summary>
    /// Đăng ký các repositories (Data Access).
    /// </summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // TODO: Đăng ký Repositories
        // services.AddScoped<IYourRepository, YourRepository>();

        return services;
    }
}

