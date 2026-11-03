using web_mvc_management.Common;

namespace web_mvc_management.Services;

/// <summary>
/// Kết quả tải ảnh lên Cloudinary CDN hoặc kho tĩnh cục bộ.
/// </summary>
public record ImageUploadResult(string Url, string PublicId, bool IsCloudinary);

/// <summary>
/// Dịch vụ quản lý tải lên và xóa hình ảnh sách (ảnh bìa, mục lục, trang đọc thử) qua Cloudinary.
/// </summary>
public interface ICloudinaryService
{
    Task<ServiceResult<ImageUploadResult>> UploadBookImageAsync(IFormFile file, CancellationToken ct = default);

    Task<ServiceResult> DeleteImageAsync(string? publicId, CancellationToken ct = default);
}

