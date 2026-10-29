using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace web_mvc_management.Util;

/// <summary>
/// Các tiện ích xử lý chuỗi: xóa dấu tiếng Việt, tạo slug, che email/sđt.
/// </summary>
public static partial class StringUtil
{
    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MultipleSpacesRegex();

    [GeneratedRegex(@"[^a-z0-9\s-]", RegexOptions.Compiled)]
    private static partial Regex InvalidSlugCharsRegex();

    /// <summary>
    /// Chuyển đổi chuỗi tiếng Việt có dấu thành không dấu (dùng để tìm kiếm không dấu).
    /// </summary>
    public static string RemoveVietnameseSigns(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd')
            .Replace('Đ', 'D');
    }

    /// <summary>
    /// Tạo URL slug thân thiện từ tiêu đề (ví dụ: "Lập trình C# cơ bản" -> "lap-trinh-c-co-ban").
    /// </summary>
    public static string ToSlug(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var withoutSigns = RemoveVietnameseSigns(text).ToLowerInvariant();
        var clean = InvalidSlugCharsRegex().Replace(withoutSigns, "");
        var singleSpaced = MultipleSpacesRegex().Replace(clean, " ").Trim();
        return singleSpaced.Replace(' ', '-');
    }

    /// <summary>
    /// Che mờ email để bảo mật thông tin (ví dụ: nguyenvana@gmail.com -> n***a@gmail.com).
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return email ?? string.Empty;
        }

        var parts = email.Split('@');
        var name = parts[0];
        var domain = parts[1];

        if (name.Length <= 2)
        {
            return $"{name[0]}*@{domain}";
        }

        return $"{name[0]}***{name[^1]}@{domain}";
    }

    /// <summary>
    /// Che mờ số điện thoại (ví dụ: 0912345678 -> 0912***678).
    /// </summary>
    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 6)
        {
            return phone ?? string.Empty;
        }

        return $"{phone[..4]}***{phone[^3..]}";
    }
}

