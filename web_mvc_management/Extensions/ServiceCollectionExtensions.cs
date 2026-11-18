using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using web_mvc_management.Constant;
using web_mvc_management.Data;
using web_mvc_management.Data.Interceptors;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models;
using web_mvc_management.Repositories;
using web_mvc_management.Services;

namespace web_mvc_management.Extensions;

public static class ServiceCollectionExtensions
{
    private static readonly FastLocalSqlConnectionInterceptor SqlConnectionInterceptor = new();
    private static readonly FastLocalSqlCommandInterceptor SqlCommandInterceptor = new();

    /// <summary>
    /// Đăng ký DbContext với kết nối bảo mật và tối ưu tốc độ kết nối IPv4 Loopback.
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", false);

        var connectionString = ResolveConnectionString(configuration);
        var commandTimeout = Debugger.IsAttached ? 300 : 60;

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    sqlOptions.CommandTimeout(commandTimeout);
                }
            )
            .AddInterceptors(SqlConnectionInterceptor, SqlCommandInterceptor)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.MultipleCollectionIncludeWarning))
        );

        return services;
    }

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var rawConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        var connectionString = !string.IsNullOrWhiteSpace(rawConnectionString) &&
            !rawConnectionString.Contains("your_db_password", StringComparison.OrdinalIgnoreCase)
            ? rawConnectionString
            : null;

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

            var hasValidPassword = string.IsNullOrWhiteSpace(user) ||
                (!string.IsNullOrWhiteSpace(pass) && !pass.Equals("your_db_password", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(db) && hasValidPassword)
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
                "Không tìm thấy chuỗi kết nối Database hợp lệ (hoặc đang giữ giá trị placeholder 'your_db_password'). " +
                "Vui lòng tạo file .env tại thư mục gốc từ .env.example và điền thông tin kết nối DB thật (ConnectionStrings__DefaultConnection hoặc DatabaseSettings__Password)."
            );
        }

        var csBuilder = new SqlConnectionStringBuilder(connectionString)
        {
            TrustServerCertificate = true,
            Encrypt = SqlConnectionEncryptOption.Optional,
            MultipleActiveResultSets = false,
            Pooling = true,
            MinPoolSize = 2,
            MaxPoolSize = 100,
            ConnectTimeout = 30,
            CommandTimeout = Debugger.IsAttached ? 300 : 60
        };

        if (csBuilder.DataSource.StartsWith("localhost", StringComparison.OrdinalIgnoreCase))
        {
            csBuilder.DataSource = "127.0.0.1" + csBuilder.DataSource["localhost".Length..];
        }

        return csBuilder.ConnectionString;
    }

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

                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync
                };
            });

        return services;
    }

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        services.AddHttpClient("Cloudinary", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        services.AddMemoryCache();
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

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        ConfigureOptions(services, configuration);

        // Domain Engines dùng chung
        services.AddScoped<ICirculationPolicy, CirculationPolicy>();
        services.AddScoped<IReservationQueueEngine, ReservationQueueEngine>();

        // Xác thực & Hạ tầng
        services.AddScoped<ICloudinaryService, CloudinaryService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddSingleton<IEmailService, EmailService>();
        services.AddSingleton<IIpRateLimiterService, IpRateLimiterService>();
        services.AddScoped<IAuthService, AuthService>();

        // Miền Danh mục sách (Catalog)
        services.AddScoped<ICatalogLookupService, CatalogLookupService>();
        services.AddScoped<IPublicCatalogService, PublicCatalogService>();
        services.AddScoped<IBookAdminService, BookAdminService>();
        services.AddScoped<IBookCopyService, BookCopyService>();
        services.AddScoped<ICatalogMasterService, CatalogMasterService>();

        // Miền Lưu thông (Circulation)
        services.AddScoped<ILoanDeskService, LoanDeskService>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<IFineService, FineService>();

        // Miền Bạn đọc (Reader)
        services.AddScoped<IReaderPortalService, ReaderPortalService>();
        services.AddScoped<IReaderAdminService, ReaderAdminService>();

        // Miền Hệ thống & Báo cáo (System & Reports)
        services.AddScoped<IStaffAdminService, StaffAdminService>();
        services.AddScoped<IReaderTypeService, ReaderTypeService>();
        services.AddScoped<ILibrarySettingService, LibrarySettingService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ILibraryInfoService, LibraryInfoService>();

        return services;
    }

    private static void ConfigureOptions(IServiceCollection services, IConfiguration? configuration)
    {
        if (configuration != null)
        {
            services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
            services.Configure<CloudinarySettings>(configuration.GetSection(CloudinarySettings.SectionName));
        }

        services.PostConfigure<EmailSettings>(options =>
        {
            var envHost = Environment.GetEnvironmentVariable("MailSettings__Host");
            if (string.IsNullOrWhiteSpace(options.Host))
                options.Host = !string.IsNullOrWhiteSpace(envHost) ? envHost : "smtp.gmail.com";

            if (int.TryParse(Environment.GetEnvironmentVariable("MailSettings__Port"), out var port) && port > 0)
                options.Port = port;

            var envUser = Environment.GetEnvironmentVariable("MailSettings__Username");
            if (string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(envUser))
                options.Username = envUser;

            var envPass = Environment.GetEnvironmentVariable("MailSettings__Password");
            if (string.IsNullOrWhiteSpace(options.Password) && !string.IsNullOrWhiteSpace(envPass))
                options.Password = envPass;

            var envSender = Environment.GetEnvironmentVariable("MailSettings__SenderDisplayName");
            if (!string.IsNullOrWhiteSpace(envSender) &&
                (string.IsNullOrWhiteSpace(options.SenderDisplayName) || options.SenderDisplayName == "Thư Viện UTC"))
            {
                options.SenderDisplayName = envSender;
            }

            if (bool.TryParse(Environment.GetEnvironmentVariable("MailSettings__Auth"), out var auth))
                options.Auth = auth;
            if (bool.TryParse(Environment.GetEnvironmentVariable("MailSettings__StartTls"), out var startTls))
                options.StartTls = startTls;
            if (bool.TryParse(Environment.GetEnvironmentVariable("MailSettings__StartTlsRequired"), out var startTlsReq))
                options.StartTlsRequired = startTlsReq;
        });

        services.PostConfigure<CloudinarySettings>(options =>
        {
            var envCloud = Environment.GetEnvironmentVariable("CloudinarySettings__CloudName");
            if (string.IsNullOrWhiteSpace(options.CloudName) && !string.IsNullOrWhiteSpace(envCloud))
                options.CloudName = envCloud;

            var envKey = Environment.GetEnvironmentVariable("CloudinarySettings__ApiKey");
            if (string.IsNullOrWhiteSpace(options.ApiKey) && !string.IsNullOrWhiteSpace(envKey))
                options.ApiKey = envKey;

            var envSecret = Environment.GetEnvironmentVariable("CloudinarySettings__ApiSecret");
            if (string.IsNullOrWhiteSpace(options.ApiSecret) && !string.IsNullOrWhiteSpace(envSecret))
                options.ApiSecret = envSecret;

            var envFolder = Environment.GetEnvironmentVariable("CloudinarySettings__Folder");
            if (!string.IsNullOrWhiteSpace(envFolder) &&
                (string.IsNullOrWhiteSpace(options.Folder) || options.Folder == "library_management/books"))
            {
                options.Folder = envFolder;
            }

            options.CloudName = options.CloudName?.Trim().Trim('"', '\'') ?? string.Empty;
            options.ApiKey = options.ApiKey?.Trim().Trim('"', '\'') ?? string.Empty;
            options.ApiSecret = options.ApiSecret?.Trim().Trim('"', '\'') ?? string.Empty;
            options.Folder = string.IsNullOrWhiteSpace(options.Folder)
                ? "library_management/books"
                : options.Folder.Trim().Trim('"', '\'').Trim('/');
        });
    }
}
