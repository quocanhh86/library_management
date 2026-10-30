using DotNetEnv;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Data;
using web_mvc_management.Extensions;
using web_mvc_management.Middlewares;

// Nạp file .env bằng thư viện chuẩn DotNetEnv (tự động duyệt ngược tìm .env ở project root/solution root)
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Đảm bảo các biến môi trường từ .env được nạp đầy đủ vào IConfiguration
builder.Configuration.AddEnvironmentVariables();

// Add services to the container (Tự động bảo vệ chống CSRF cho các phương thức POST/PUT/DELETE)
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Database (Ưu tiên cấu hình từ .env)
builder.Services.AddDatabase(builder.Configuration);

// Common Infrastructure & Repositories
builder.Services.AddInfrastructureServices();
builder.Services.AddRepositories();
builder.Services.AddApplicationServices();

var app = builder.Build();

// Seed initial clean demo data (Có bắt lỗi để không làm crash app nếu DB chưa sẵn sàng)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DbInitializer.InitializeAsync(context);
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
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Global Exception Handling
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();

// Stateful Session và Xác thực Cookie Authentication
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
