namespace web_mvc_management.Helper;

/// <summary>
/// Chuẩn hóa sinh mã và tách hậu tố tìm kiếm phiếu mượn thống nhất toàn hệ thống (PM-yyyyMMdd-XXXX).
/// </summary>
public static class SlipCodeHelper
{
    public const string Prefix = "PM-";

    public static string Format(Guid slipId, DateTimeOffset borrowedAt) =>
        $"PM-{borrowedAt:yyyyMMdd}-{slipId.ToString("N")[..4].ToUpperInvariant()}";

    public static string ExtractSearchGuidPrefix(string keyword)
    {
        var trimmed = keyword.Trim();
        if (trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var parts = trimmed.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                return parts[^1];
            }
        }
        return trimmed;
    }
}
