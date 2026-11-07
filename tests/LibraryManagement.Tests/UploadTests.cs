using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using web_mvc_management.Models;
using web_mvc_management.Services;

namespace LibraryManagement.Tests;

public class UploadTests
{
    [Theory]
    [InlineData("file.exe", "image/png", 1)]
    [InlineData("file.png", "text/html", 1)]
    [InlineData("file.png", "image/png", 0)]
    [InlineData("file.png", "image/png", 10485761)]
    public async Task InvalidOrOversizedUploadIsRejected(string name, string type, int length)
    {
        var environment = new TestEnvironment();
        var service = new CloudinaryService(Options.Create(new CloudinarySettings()), environment, NullLogger<CloudinaryService>.Instance);
        using var stream = new MemoryStream([1]);
        var file = new FormFile(stream, 0, length, "image", name) { Headers = new HeaderDictionary(), ContentType = type };
        Assert.False((await service.UploadBookImageAsync(file)).Succeeded);
        Assert.False(Directory.Exists(environment.WebRootPath));
    }
    [Fact] public async Task LocalImageUploadAndDeleteRoundTrip()
    {
        var environment = new TestEnvironment();
        var service = new CloudinaryService(Options.Create(new CloudinarySettings()), environment, NullLogger<CloudinaryService>.Instance);
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
        try
        {
            using var stream = new MemoryStream(bytes);
            var file = new FormFile(stream, 0, bytes.Length, "image", "cover.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
            var result = await service.UploadBookImageAsync(file);
            Assert.True(result.Succeeded);
            Assert.False(result.Data!.IsCloudinary);
            var path = Path.Combine(environment.WebRootPath, result.Data.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
            Assert.True((await service.DeleteImageAsync(result.Data.PublicId)).Succeeded);
            Assert.False(File.Exists(path));
        }
        finally
        {
            var root = Path.GetFullPath(environment.WebRootPath);
            var parent = Path.GetFullPath(AppContext.BaseDirectory) + "upload-test-";
            if (!root.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe cleanup path");
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "upload-test-" + Guid.NewGuid().ToString("N"));
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = "Development";
    }
}
