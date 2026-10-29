using System.Globalization;

namespace web_mvc_management.Helper;

/// <summary>
/// Hỗ trợ định dạng tiền tệ Việt Nam Đồng (VND).
/// </summary>
public static class CurrencyHelper
{
    private static readonly CultureInfo VnCulture = new("vi-VN");

    public static string ToVnd(decimal? amount)
    {
        if (!amount.HasValue)
        {
            return "0 ₫";
        }
        return string.Format(VnCulture, "{0:C0}", amount.Value);
    }

    public static string ToVnd(double? amount)
    {
        return ToVnd(amount.HasValue ? (decimal)amount.Value : null);
    }
}

