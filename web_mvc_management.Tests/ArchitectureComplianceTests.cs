using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using web_mvc_management.Controllers;
using web_mvc_management.Data;
using web_mvc_management.Entity;
using web_mvc_management.Extensions;
using web_mvc_management.Repositories;
using web_mvc_management.Services;
using Xunit;

namespace web_mvc_management.Tests;

public class ArchitectureComplianceTests
{
    private static readonly Assembly WebAssembly = typeof(HomeController).Assembly;

    [Fact]
    public void Controllers_ShouldNotInjectDbContextOrUnitOfWork()
    {
        var controllerTypes = WebAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(controllerTypes);

        foreach (var controller in controllerTypes)
        {
            var paramTypes = controller.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => p.ParameterType)
                .ToList();

            Assert.DoesNotContain(paramTypes, t => t == typeof(ApplicationDbContext));
            Assert.DoesNotContain(paramTypes, t => t == typeof(IUnitOfWork));
        }
    }

    [Fact]
    public void PublicApplicationServiceInterfaces_ShouldNotExposeDomainEntities()
    {
        Type[] internalDomainEngines = [typeof(ICirculationPolicy), typeof(IReservationQueueEngine)];

        var serviceInterfaces = WebAssembly.GetTypes()
            .Where(t => t.IsInterface
                        && t.Namespace == "web_mvc_management.Services"
                        && !internalDomainEngines.Contains(t))
            .ToList();

        Assert.True(serviceInterfaces.Count >= 18);

        foreach (var iface in serviceInterfaces)
        {
            foreach (var method in iface.GetMethods())
            {
                AssertNoEntityType(method.ReturnType, $"{iface.Name}.{method.Name} return type");
                foreach (var param in method.GetParameters())
                {
                    AssertNoEntityType(param.ParameterType, $"{iface.Name}.{method.Name}({param.Name})");
                }
            }
        }
    }

    [Fact]
    public void ServiceCollectionExtensions_ShouldResolveAllRegisteredApplicationServices()
    {
        using var provider = BuildValidatedServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<ICirculationPolicy>());
        Assert.NotNull(sp.GetRequiredService<IReservationQueueEngine>());
        Assert.NotNull(sp.GetRequiredService<ICatalogLookupService>());
        Assert.NotNull(sp.GetRequiredService<IPublicCatalogService>());
        Assert.NotNull(sp.GetRequiredService<IBookAdminService>());
        Assert.NotNull(sp.GetRequiredService<IBookCopyService>());
        Assert.NotNull(sp.GetRequiredService<ICatalogMasterService>());
        Assert.NotNull(sp.GetRequiredService<ILoanDeskService>());
        Assert.NotNull(sp.GetRequiredService<IReservationService>());
        Assert.NotNull(sp.GetRequiredService<IFineService>());
        Assert.NotNull(sp.GetRequiredService<IReaderPortalService>());
        Assert.NotNull(sp.GetRequiredService<IReaderAdminService>());
        Assert.NotNull(sp.GetRequiredService<IStaffAdminService>());
        Assert.NotNull(sp.GetRequiredService<IReaderTypeService>());
        Assert.NotNull(sp.GetRequiredService<ILibrarySettingService>());
        Assert.NotNull(sp.GetRequiredService<IDashboardService>());
        Assert.NotNull(sp.GetRequiredService<IReportService>());
        Assert.NotNull(sp.GetRequiredService<ILibraryInfoService>());
        Assert.NotNull(sp.GetRequiredService<IAuthService>());
        Assert.NotNull(sp.GetRequiredService<IOtpService>());
        Assert.NotNull(sp.GetRequiredService<ICloudinaryService>());
        Assert.NotNull(sp.GetRequiredService<IEmailService>());
        Assert.NotNull(sp.GetRequiredService<IIpRateLimiterService>());
    }

    [Fact]
    public void AllControllers_ShouldBeInstantiableFromDiContainer()
    {
        var controllerTypes = WebAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(controllerTypes);

        using var provider = BuildValidatedServiceProvider();
        using var scope = provider.CreateScope();

        foreach (var controllerType in controllerTypes)
        {
            var instance = ActivatorUtilities.CreateInstance(scope.ServiceProvider, controllerType);
            Assert.NotNull(instance);
        }
    }

    private static ServiceProvider BuildValidatedServiceProvider()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=127.0.0.1,1433;Database=FakeDb;User Id=sa;Password=FakeStrongPassword!123;TrustServerCertificate=True;"
            })
            .Build();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment());
        services.AddSingleton<IWebHostEnvironment>(new FakeHostEnvironment());
        services.AddDatabase(config);
        services.AddInfrastructureServices();
        services.AddRepositories();
        services.AddApplicationServices(config);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public void RazorViews_ShouldNotReferenceDomainEntitiesDirectly()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "web_mvc_management", "Views"));
        Assert.True(Directory.Exists(repoRoot), $"Không tìm thấy thư mục Views: {repoRoot}");

        var viewFiles = Directory.GetFiles(repoRoot, "*.cshtml", SearchOption.AllDirectories);
        Assert.NotEmpty(viewFiles);

        foreach (var file in viewFiles)
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("web_mvc_management.Entity.", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ChromeUiRegressionGuards_ShouldPreventBindingAndUnobtrusiveValidationBugs()
    {
        // 1. ReaderController.CancelReservation phải nhận đúng tham số reservationId khớp với Views/Reader/Reservations.cshtml
        var cancelMethod = typeof(ReaderController).GetMethod(nameof(ReaderController.CancelReservation));
        Assert.NotNull(cancelMethod);
        Assert.Contains(cancelMethod!.GetParameters(), p => p.Name == "reservationId" && p.ParameterType == typeof(Guid));

        // 2. BookUpsertViewModel.GalleryFiles và DeleteImageIds phải là Nullable để ASP.NET Core NRT không tự sinh data-val-required chặn submit form khi để trống
        var nullabilityCtx = new NullabilityInfoContext();
        var galleryProp = typeof(web_mvc_management.Models.ViewModels.AdminCatalog.BookUpsertViewModel).GetProperty("GalleryFiles")!;
        var deleteIdsProp = typeof(web_mvc_management.Models.ViewModels.AdminCatalog.BookUpsertViewModel).GetProperty("DeleteImageIds")!;
        Assert.Equal(NullabilityState.Nullable, nullabilityCtx.Create(galleryProp).WriteState);
        Assert.Equal(NullabilityState.Nullable, nullabilityCtx.Create(deleteIdsProp).WriteState);
    }

    private static void AssertNoEntityType(Type type, string context)
    {
        if (type.Namespace == typeof(BaseEntity).Namespace)
        {
            Assert.Fail($"Service interface vi phạm Strict ViewModel: {context} làm lộ Entity '{type.Name}'.");
        }

        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                AssertNoEntityType(arg, context);
            }
        }
    }

    private sealed class FakeHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "web_mvc_management";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
