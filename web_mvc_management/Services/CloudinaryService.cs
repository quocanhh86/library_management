using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using web_mvc_management.Common;
using web_mvc_management.Models;
using web_mvc_management.Util;

namespace web_mvc_management.Services;

/// <summary>
/// Triển khai dịch vụ lưu trữ hình ảnh sách qua Cloudinary REST API (SHA-1 Signed Upload).
/// Tự động kiểm tra định dạng/dung lượng/Magic Bytes/Polyglot/SVG XSS an toàn và fallback lưu trữ tại wwwroot/uploads/books trong môi trường Dev.
/// </summary>
public class CloudinaryService(
    IOptions<CloudinarySettings> cloudinaryOptions,
    IWebHostEnvironment env,
    ILogger<CloudinaryService> logger) : ICloudinaryService
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg"
    };
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif", "image/svg+xml"
    };

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly CloudinarySettings _settings = cloudinaryOptions.Value;

    public async Task<ServiceResult<ImageUploadResult>> UploadBookImageAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            return ServiceResult<ImageUploadResult>.Failure("Tệp hình ảnh tải lên đang trống.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return ServiceResult<ImageUploadResult>.Failure($"Dung lượng ảnh \"{Path.GetFileName(file.FileName)}\" vượt quá giới hạn 10 MB.");
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext) || !AllowedContentTypes.Contains(file.ContentType))
        {
            return ServiceResult<ImageUploadResult>.Failure($"Định dạng tệp \"{Path.GetFileName(file.FileName)}\" không hợp lệ. Chỉ chấp nhận ảnh .jpg, .jpeg, .png, .webp, .gif hoặc .svg.");
        }

        // Kiểm tra sâu Magic Bytes, Double Extension, Polyglot File & SVG XSS
        var (isValidFile, fileSecurityError) = await InputSanitizer.ValidateUploadedImageAsync(file, MaxFileSizeBytes, ct);
        if (!isValidFile)
        {
            logger.LogWarning("[FILE UPLOAD BLOCKED] Từ chối tệp tải lên '{FileName}': {Reason}", file.FileName, fileSecurityError);
            return ServiceResult<ImageUploadResult>.Failure(fileSecurityError);
        }

        if (_settings.IsConfigured)
        {
            try
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                var folder = string.IsNullOrWhiteSpace(_settings.Folder) ? "library_management/books" : _settings.Folder.Trim();
                var signPayload = $"folder={folder}&timestamp={timestamp}{_settings.ApiSecret}";
                var signature = ComputeSha1Hex(signPayload);

                using var form = new MultipartFormDataContent();
                await using var fileStream = file.OpenReadStream();
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

                form.Add(streamContent, "file", Path.GetFileName(file.FileName));
                form.Add(new StringContent(_settings.ApiKey), "api_key");
                form.Add(new StringContent(timestamp), "timestamp");
                form.Add(new StringContent(folder), "folder");
                form.Add(new StringContent(signature), "signature");

                var uploadUrl = $"https://api.cloudinary.com/v1_1/{_settings.CloudName}/image/upload";
                using var response = await Http.PostAsync(uploadUrl, form, ct);
                var body = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(body);
                    var secureUrl = doc.RootElement.GetProperty("secure_url").GetString();
                    var publicId = doc.RootElement.GetProperty("public_id").GetString();

                    if (!string.IsNullOrWhiteSpace(secureUrl) && !string.IsNullOrWhiteSpace(publicId))
                    {
                        logger.LogInformation("[CLOUDINARY OK] Đã tải ảnh sách lên Cloudinary: {Url} (PublicId: {PublicId})", secureUrl, publicId);
                        return ServiceResult<ImageUploadResult>.Success(new ImageUploadResult(secureUrl, publicId, IsCloudinary: true));
                    }
                }

                logger.LogWarning("[CLOUDINARY WARN] Phản hồi lỗi từ Cloudinary ({Status}): {Body}. Chuyển sang lưu cục bộ.", (int)response.StatusCode, body);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[CLOUDINARY WARN] Không thể kết nối Cloudinary ({CloudName}), tự động fallback lưu ảnh cục bộ.", _settings.CloudName);
            }
        }

        // Lưu trữ tại wwwroot/uploads/books khi chưa điền khóa Cloudinary hoặc fallback trong môi trường Dev
        var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "books");
        Directory.CreateDirectory(uploadsDir);

        var safeFileName = $"{DateTime.UtcNow:yyyyMMdd}_{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploadsDir, safeFileName);

        await using (var outStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(outStream, ct);
        }

        var localUrl = $"/uploads/books/{safeFileName}";
        var localPublicId = $"local:{safeFileName}";
        logger.LogInformation("[LOCAL STORAGE] Đã lưu ảnh sách tại {Url} (Cấu hình CloudinarySettings trong .env để đồng bộ lên Cloud CDN)", localUrl);

        return ServiceResult<ImageUploadResult>.Success(new ImageUploadResult(localUrl, localPublicId, IsCloudinary: false));
    }

    public async Task<ServiceResult> DeleteImageAsync(string? publicId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            return ServiceResult.Success();
        }

        if (publicId.StartsWith("local:", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(publicId["local:".Length..]);
            var fullPath = Path.Combine(env.WebRootPath, "uploads", "books", fileName);
            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Không thể xóa tệp ảnh cục bộ: {Path}", fullPath);
                }
            }
            return ServiceResult.Success();
        }

        if (_settings.IsConfigured)
        {
            try
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                var signPayload = $"public_id={publicId}&timestamp={timestamp}{_settings.ApiSecret}";
                var signature = ComputeSha1Hex(signPayload);

                using var form = new MultipartFormDataContent
                {
                    { new StringContent(publicId), "public_id" },
                    { new StringContent(_settings.ApiKey), "api_key" },
                    { new StringContent(timestamp), "timestamp" },
                    { new StringContent(signature), "signature" }
                };

                var destroyUrl = $"https://api.cloudinary.com/v1_1/{_settings.CloudName}/image/destroy";
                await Http.PostAsync(destroyUrl, form, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không thể xóa ảnh trên Cloudinary với PublicId: {PublicId}", publicId);
            }
        }

        return ServiceResult.Success();
    }

    private static string ComputeSha1Hex(string input)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
