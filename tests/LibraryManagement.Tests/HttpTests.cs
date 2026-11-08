using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using web_mvc_management.Controllers;
using web_mvc_management.Extensions;
using web_mvc_management.Middlewares;

namespace LibraryManagement.Tests;

// Uses real MVC controllers, Razor views, DI, SQL Server, authorization and antiforgery.
// Authentication headers are test-only; production cookie/OTP is tested separately.
public sealed class HttpFixture : IAsyncLifetime
{
    public TestDatabase Database { get; } = new();
    public TestServer Server { get; private set; } = null!;
    private IHost host = null!;
    public Guid SlipId { get; private set; }
    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        SlipId = (await Database.Circulation.CreateBorrowSlipAsync(new() { ReaderId = Database.Reader.Id, Barcodes = [Database.Copy.Barcode] })).Data;
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../web_mvc_management"));
        host = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
            .UseEnvironment("Development").UseContentRoot(root)
            .ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = Database.ConnectionString }))
            .ConfigureServices((context, services) =>
            {
                services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute())).AddApplicationPart(typeof(HomeController).Assembly);
                services.AddDatabase(context.Configuration);
                services.AddCookieAuthentication();
                services.AddInfrastructureServices();
                services.AddRepositories();
                services.AddApplicationServices(context.Configuration);
                services.AddAuthentication(o => { o.DefaultAuthenticateScheme = "Test"; o.DefaultChallengeScheme = "Test"; o.DefaultForbidScheme = "Test"; }).AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
            })
            .Configure(app =>
            {
                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.UseStaticFiles(); app.UseRouting(); app.UseSession(); app.UseAuthentication(); app.UseAuthorization();
                app.UseEndpoints(e => e.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"));
            })).StartAsync();
        Server = host.GetTestServer();
    }
    public async Task DisposeAsync() { await host.StopAsync(); host.Dispose(); await Database.DisposeAsync(); }
    public HttpClient Client(string? role = null)
    {
        var client = Server.CreateClient();
        if (role != null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
            client.DefaultRequestHeaders.Add("X-Test-User", Database.User.CurrentUserId.ToString());
        }
        return client;
    }
}

public sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].ToString();
        if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.Name, "Tester"), new Claim(ClaimTypes.NameIdentifier, Request.Headers["X-Test-User"].ToString())], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}

public class HttpTests(HttpFixture fixture) : IClassFixture<HttpFixture>
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Books")]
    [InlineData("/Books?keyword=Test&pageNo=0")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/StaffLogin")]
    [InlineData("/Account/Register")]
    public async Task PublicPagesRender(string path)
    {
        using var client = fixture.Client();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType!.ToString());
    }
    [Theory]
    [InlineData("/Admin/Books")]
    [InlineData("/Admin/Books/Create")]
    [InlineData("/Admin/BookCopies")]
    [InlineData("/Admin/Categories")]
    [InlineData("/Admin/Authors")]
    [InlineData("/Admin/Publishers")]
    [InlineData("/Admin/Shelves")]
    [InlineData("/Admin/Readers")]
    [InlineData("/Admin/ReaderTypes")]
    [InlineData("/System/Staff")]
    [InlineData("/System/Settings")]
    [InlineData("/Circulation/Borrow")]
    [InlineData("/Circulation/Borrow?card=TEST001")]
    [InlineData("/Circulation/Return?card=TEST001")]
    [InlineData("/Circulation/History")]
    [InlineData("/Reservations")]
    [InlineData("/Fines")]
    [InlineData("/Reports")]
    [InlineData("/Reports/Print")]
    [InlineData("/Reports/ExportOverdueCsv")]
    public async Task AdminPagesRenderAndRejectAnonymousAndReaders(string path)
    {
        using var admin = fixture.Client("Admin");
        var response = await admin.GetAsync(path);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        using var reader = fixture.Client("Reader");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync(path)).StatusCode);
    }
    [Theory]
    [InlineData("/Circulation/Borrow")]
    [InlineData("/Circulation/Return")]
    [InlineData("/Circulation/Renew")]
    [InlineData("/Reservations/Create")]
    [InlineData("/Reservations/Cancel")]
    [InlineData("/Fines/Pay")]
    [InlineData("/Fines/Waive")]
    [InlineData("/Admin/Books/Create")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    public async Task PostWithoutAntiforgeryIsRejected(string path)
    {
        using var client = fixture.Client("Admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(path, new FormUrlEncodedContent([]))).StatusCode);
    }
    [Theory]
    [InlineData("/System/Staff")]
    [InlineData("/System/Settings")]
    public async Task LibrarianCannotAdministerSystem(string path)
    {
        using var client = fixture.Client("Librarian");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }
    [Fact] public async Task PopulatedDetailPrintAndEditViewsRender()
    {
        using var client = fixture.Client("Admin");
        foreach (var path in new[] { $"/Circulation/History/Detail/{fixture.SlipId}", $"/Circulation/History/Print/{fixture.SlipId}", $"/Admin/Books/Edit/{fixture.Database.Copy.BookId}", $"/Books/Detail/{fixture.Database.Copy.BookId}" })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        }
    }
    [Fact] public async Task ValidAntiforgeryFormExecutesAndPersistsCategory()
    {
        using var client = fixture.Client("Admin");
        var page = await client.GetAsync("/Admin/Categories");
        var html = await page.Content.ReadAsStringAsync();
        var match = System.Text.RegularExpressions.Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success);
        client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", page.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
        var response = await client.PostAsync("/Admin/Categories/Save", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value), ["Name"] = "HTTP form category", ["Status"] = "ACTIVE" }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var db = fixture.Database.NewContext();
        Assert.True(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.Categories, x => x.Name == "HTTP form category"));
    }
}
