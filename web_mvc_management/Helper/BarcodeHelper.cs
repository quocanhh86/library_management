using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace web_mvc_management.Helper;

/// <summary>
/// Hỗ trợ tạo và kiểm tra định dạng mã vạch sách (Barcode).
/// </summary>
public static partial class BarcodeHelper
{
    [GeneratedRegex(@"^BC-\d{8}-[A-Z0-9]{4}$", RegexOptions.Compiled)]
    private static partial Regex BarcodePattern();

    /// <summary>
    /// Sinh mã vạch tự động theo format: BC-YYYYMMDD-XXXX
    /// Ví dụ: BC-20261030-A8F2
    /// </summary>
    public static string GenerateBarcode()
    {
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var randomBytes = RandomNumberGenerator.GetBytes(2);
        var randomHex = Convert.ToHexString(randomBytes);
        return $"BC-{datePart}-{randomHex}";
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của mã vạch.
    /// </summary>
    public static bool IsValidBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return false;
        }
        return BarcodePattern().IsMatch(barcode.Trim());
    }
}

