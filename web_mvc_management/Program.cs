using DotNetEnv;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Data;
using web_mvc_management.Extensions;
using web_mvc_management.Middlewares;

// Bật Managed Networking cho Microsoft.Data.SqlClient trên Windows (.NET 10.0)
// nhằm loại bỏ hoàn toàn lỗi treo/timeout bất đồng bộ của thư viện C++ native SNI.dll (Win32Exception: The wait operation timed out)
AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

// Nạp file .env tại thư mục gốc dự án (duyệt ngược từ CWD và fallback từ AppContext.BaseDirectory)
Env.TraversePath().Load();
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")) &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DatabaseSettings__Host")))
{
    var baseDir = new DirectoryInfo(AppContext.BaseDirectory);
    while (baseDir != null)
    {
        var candidateEnv = Path.Combine(baseDir.FullName, ".env");
        if (File.Exists(candidateEnv))
        {
            Env.Load(candidateEnv);
            break;
        }
        baseDir = baseDir.Parent;
    }
}

var builder = WebApplication.CreateBuilder(args);

// Đảm bảo các biến môi trường phân cấp (__) từ .env được nạp đầy đủ vào IConfiguration
builder.Configuration.AddEnvironmentVariables();

// Add services to the container:
// 1. Tự động bảo vệ chống CSRF (AutoValidateAntiforgeryTokenAttribute)
// 2. Tự động làm sạch (Sanitize) & chặn mã độc HTML/JS/SQLi/Emoji/File giả mạo khi bị can thiệp F12 (InputSanitizationActionFilter)
// 3. Khi gắn Debugger (F5), tránh truyền RequestAborted gây dừng trình gỡ lỗi bởi TaskCanceledException khi chuyển trang nhanh
builder.Services.AddControllersWithViews(options =>
{
    options.ModelBinderProviders.Insert(0, new SafeCancellationTokenModelBinderProvider());
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<InputSanitizationActionFilter>();
});

// Cấu hình Antiforgery Cookie bảo mật (HttpOnly, SameSite=Lax, Header X-CSRF-TOKEN cho AJAX)
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = ".LibraryManagement.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Cấu hình CORS nghiêm ngặt: mặc định khóa toàn bộ Cross-Origin từ domain lạ
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => false)
              .WithMethods("GET", "POST");
    });
});

// Database & Cookie Authentication (Ưu tiên cấu hình phân cấp từ .env)
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddCookieAuthentication();

// Common Infrastructure, Repositories & Application Services
builder.Services.AddInfrastructureServices();
builder.Services.AddRepositories();
builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

// Seed initial clean demo data (Có bắt lỗi để không làm crash app nếu DB chưa sẵn sàng)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        await DbInitializer.InitializeAsync(context, configuration);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Chưa thể kết nối Database để seed dữ liệu mẫu. Vui lòng kiểm tra file .env và cập nhật migration.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Global Exception Handling
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Bảo vệ tầng HTTP Request/Response: Security Headers (CSP, X-Frame-Options, nosniff), chặn Cross-Site POST (Sec-Fetch-Site/Origin), chặn Path Traversal & Null-byte
app.UseMiddleware<RequestSecurityMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();

app.UseCors();

// Stateful Session và Xác thực Cookie Authentication
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
