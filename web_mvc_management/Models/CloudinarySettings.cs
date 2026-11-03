namespace web_mvc_management.Models;

/// <summary>
/// Cấu hình tích hợp dịch vụ lưu trữ ảnh đám mây Cloudinary (bind từ Section CloudinarySettings trong .env).
/// </summary>
public class CloudinarySettings
{
    public const string SectionName = "CloudinarySettings";

    public string CloudName { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    public string Folder { get; set; } = "library_management/books";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(CloudName) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ApiSecret) &&
        !CloudName.Equals("your_cloud_name", StringComparison.OrdinalIgnoreCase) &&
        !ApiKey.Equals("your_api_key", StringComparison.OrdinalIgnoreCase) &&
        !ApiSecret.Equals("your_api_secret", StringComparison.OrdinalIgnoreCase);
}

