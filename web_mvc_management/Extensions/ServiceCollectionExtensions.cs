using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using web_mvc_management.Constant;
using web_mvc_management.Data;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models;
using web_mvc_management.Repositories;
using web_mvc_management.Services;

namespace web_mvc_management.Extensions;

/// <summary>
/// Interceptor mở kết nối SQL Server qua đường dẫn đồng bộ (0ms từ Connection Pool)
/// để tránh lỗi bất đồng bộ của Microsoft.Data.SqlClient 5.1.6 trên .NET 10.0
/// (TaskCanceledException trong OpenInternalAsync và Win32Exception wait timeout trên SNI).
/// </summary>
public sealed class FastLocalSqlConnectionInterceptor : DbConnectionInterceptor
{
    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }
        return InterceptionResult.Suppress();
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }
        return ValueTask.FromResult(InterceptionResult.Suppress());
    }

    public override ValueTask<InterceptionResult> ConnectionClosingAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        if (connection.State != System.Data.ConnectionState.Closed)
        {
            connection.Close();
        }
        return ValueTask.FromResult(InterceptionResult.Suppress());
    }
}

/// <summary>
/// Bọc DbDataReader đồng bộ để mọi thao tác ReadAsync / IsDBNullAsync / GetFieldValueAsync / DisposeAsync
/// của EF Core đều chạy trên luồng đồng bộ nhất quán với command.ExecuteReader(), không trộn lẫn Sync/Async trên TDS stream.
/// </summary>
public sealed class SyncDbDataReader(DbDataReader inner) : DbDataReader
{
    public override object this[int ordinal] => inner[ordinal];
    public override object this[string name] => inner[name];
    public override int Depth => inner.Depth;
    public override int FieldCount => inner.FieldCount;
    public override bool HasRows => inner.HasRows;
    public override bool IsClosed => inner.IsClosed;
    public override int RecordsAffected => inner.RecordsAffected;
    public override int VisibleFieldCount => inner.VisibleFieldCount;

    public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);
    public override byte GetByte(int ordinal) => inner.GetByte(ordinal);
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
    public override char GetChar(int ordinal) => inner.GetChar(ordinal);
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
    public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);
    public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);
    public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);
    public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);
    public override System.Collections.IEnumerator GetEnumerator() => inner.GetEnumerator();
    public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);
    public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);
    public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);
    public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);
    public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);
    public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);
    public override string GetName(int ordinal) => inner.GetName(ordinal);
    public override int GetOrdinal(string name) => inner.GetOrdinal(name);
    public override string GetString(int ordinal) => inner.GetString(ordinal);
    public override object GetValue(int ordinal) => inner.GetValue(ordinal);
    public override int GetValues(object[] values) => inner.GetValues(values);
    public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);
    public override bool NextResult() => inner.NextResult();
    public override bool Read() => inner.Read();
    public override T GetFieldValue<T>(int ordinal) => inner.GetFieldValue<T>(ordinal);

    public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(inner.Read());
    public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => Task.FromResult(inner.NextResult());
    public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) => Task.FromResult(inner.IsDBNull(ordinal));
    public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken) => Task.FromResult(inner.GetFieldValue<T>(ordinal));

    public override void Close() => inner.Close();
    public override Task CloseAsync()
    {
        inner.Close();
        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        inner.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Interceptor thực thi DbCommand qua đường dẫn đồng bộ trên kết nối Loopback cục bộ
/// nhằm triệt tiêu hoàn toàn lỗi Win32Exception (258) wait timeout của native SNI.dll
/// trong EndExecuteNonQueryAsync / EndExecuteReaderAsync trên .NET 10.0.
/// </summary>
public sealed class FastLocalSqlCommandInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        var reader = new SyncDbDataReader(command.ExecuteReader());
        return InterceptionResult<DbDataReader>.SuppressWithResult(reader);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        var reader = new SyncDbDataReader(command.ExecuteReader());
        return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(reader));
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var rows = command.ExecuteNonQuery();
        return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(rows));
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        var scalar = command.ExecuteScalar();
        return ValueTask.FromResult(InterceptionResult<object>.SuppressWithResult(scalar!));
    }
}

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

    /// <summary>
    /// Trích xuất chuỗi kết nối an toàn từ IConfiguration theo chuẩn phân cấp ASP.NET Core (ConnectionStrings / DatabaseSettings).
    /// </summary>
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

        // Tối ưu kết nối cục bộ trên Windows: chuyển localhost -> 127.0.0.1 để tránh delay 1.3s do fallback IPv6 (::1),
        // tắt Encrypt/MARS trên loopback để tránh treo SNI/SslStream và TaskCanceledException trong OpenInternalAsync.
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

        // Cấu hình Memory Cache, Distributed Memory Cache & Stateful Session (MVC Server-Side State)
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

        services.AddScoped<ICloudinaryService, CloudinaryService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddSingleton<IEmailService, EmailService>();
        services.AddSingleton<IIpRateLimiterService, IpRateLimiterService>();
        services.AddScoped<IAuthService, AuthService>();

        // Nghiệp vụ Danh mục sách, Quy định thư viện & Quản trị hệ thống kết nối SQL Server qua UnitOfWork
        services.AddScoped<IBookService, BookService>();
        services.AddScoped<ILibraryInfoService, LibraryInfoService>();
        services.AddScoped<ICatalogMasterService, CatalogMasterService>();
        services.AddScoped<ISystemAdminService, SystemAdminService>();
        services.AddScoped<ICirculationService, CirculationService>();
        services.AddScoped<IReaderAdminService, ReaderAdminService>();
        services.AddScoped<IReaderPortalService, ReaderPortalService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
