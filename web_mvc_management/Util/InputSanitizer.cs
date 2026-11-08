using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace web_mvc_management.Util;

/// <summary>
/// Bộ công cụ làm sạch dữ liệu đầu vào (Input Sanitizer) và kiểm định tệp tải lên (File Upload Inspector):
/// - Chống can thiệp F12 DevTools (gỡ maxlength/pattern/required, sửa hidden/select, bơm HTML/Script/SVG).
/// - Chuẩn hóa Unicode, loại bỏ Null-byte, ký tự điều khiển, Zero-Width, BiDi Override và Zalgo text.
/// - Chặn/lọc Emoji & Icon ở các trường mã định danh, tài khoản, email, SĐT, ISBN, Barcode.
/// - Kiểm tra chữ ký nhị phân (Magic Bytes), chống Polyglot File và SVG XSS khi upload ảnh.
/// </summary>
public static partial class InputSanitizer
{
    // 1. Phát hiện thẻ HTML / Script / Event Handler / URI độc hại
    [GeneratedRegex(@"<\s*(script|style|iframe|object|embed|svg|math|applet|template)\b[^>]*>[\s\S]*?<\s*\/\s*\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DangerousPairedBlockRegex();

    [GeneratedRegex(@"<\s*\/?\s*(script|iframe|object|embed|svg|math|style|link|meta|base|form|input|button|textarea|select|option|applet|body|html|head|frame|frameset|layer|ilayer|bgsound|marquee|blink|xss|img|video|audio|source|details|dialog|template)\b[^>]*>|<\s*[a-zA-Z!/?][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\bon[a-z]{2,25}\s*=|javascript\s*:|vbscript\s*:|data\s*:\s*(text\/html|image\/svg\+xml|application\/javascript|application\/x-javascript)|expression\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DangerousJsPatternRegex();

    // 2. Phát hiện chuỗi tấn công SQL Injection kinh điển khi người dùng cố tình nhập qua F12
    [GeneratedRegex(@"(\b(UNION\s+ALL\s+SELECT|UNION\s+SELECT|DROP\s+TABLE|ALTER\s+TABLE|EXEC\s*\(|EXECUTE\s+sp_|xp_cmdshell|WAITFOR\s+DELAY)\b|--\s*$|;\s*(DROP|DELETE|UPDATE|INSERT)\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DangerousSqlPatternRegex();

    // 3. Ký tự điều khiển ASCII (trừ \r, \n, \t), Zero-Width và BiDi Override (dùng để giả mạo tên file/văn bản)
    [GeneratedRegex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F\u200B-\u200F\u202A-\u202E\u2060-\u206F\uFEFF]", RegexOptions.Compiled)]
    private static partial Regex ControlAndInvisibleCharsRegex();

    // 4. Zalgo text (quá 2 dấu thanh/dấu phụ chồng lên nhau trên 1 ký tự làm vỡ giao diện)
    [GeneratedRegex(@"\p{Mn}{3,}", RegexOptions.Compiled)]
    private static partial Regex ZalgoCombiningMarksRegex();

    // 5. Emoji, Pictographs, Surrogate Pairs và Dingbats
    [GeneratedRegex(@"(\u00a9|\u00ae|[\u2000-\u3300]|\ud83c[\ud000-\udfff]|\ud83d[\ud000-\udfff]|\ud83e[\ud000-\udfff]|[\u2600-\u27bf]|[\ufe00-\ufe0f])", RegexOptions.Compiled)]
    private static partial Regex EmojiAndIconRegex();

    // 6. Các trường định danh/mã/email/SĐT nghiêm cấm chứa Emoji, Icon hoặc ký tự lạ
    private static readonly HashSet<string> StrictNoEmojiFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Username", "UsernameOrEmail", "Email", "Phone", "Code", "CardNo", "Barcode",
        "Isbn", "ShelfCode", "MemberCode", "OtpCode", "Role", "Status", "CardStatus",
        "ConfirmCode", "PaymentMethod", "ReturnCondition", "FineStatus", "ReservationStatus"
    };

    /// <summary>
    /// Kiểm tra xem chuỗi có chứa payload tấn công (HTML/Script/Event/URI độc hại/SQLi) hay không.
    /// </summary>
    public static bool TryDetectMaliciousInput(string? input, string? fieldName, out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // Chuẩn hóa Unicode FormKC để bắt các ký tự full-width như ＜script＞ (U+FF1C)
        var normalized = NormalizeUnicodeSafe(input, FormKC: true);

        if (input.Contains('\0'))
        {
            reason = $"Trường \"{fieldName ?? "Dữ liệu"}\" chứa ký tự Null-byte không hợp lệ.";
            return true;
        }

        if (HtmlTagRegex().IsMatch(normalized) || DangerousJsPatternRegex().IsMatch(normalized))
        {
            reason = $"Trường \"{fieldName ?? "Dữ liệu"}\" không được chứa thẻ HTML, mã Script hoặc thuộc tính sự kiện.";
            return true;
        }

        if (DangerousSqlPatternRegex().IsMatch(normalized))
        {
            reason = $"Trường \"{fieldName ?? "Dữ liệu"}\" chứa cú pháp truy vấn bị cấm.";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(fieldName) &&
            StrictNoEmojiFields.Contains(fieldName) &&
            ContainsEmojiOrSurrogate(input))
        {
            reason = $"Trường \"{fieldName}\" không được chứa biểu tượng (Emoji/Icon) hoặc ký tự đặc biệt ngoài bảng chữ cái.";
            return true;
        }

        return false;
    }

    /// <summary>
    /// Làm sạch chuỗi đầu vào: chuẩn hóa Unicode FormC, xóa ký tự điều khiển/Zero-width/BiDi,
    /// loại bỏ Zalgo, lọc thẻ HTML/JS, và loại bỏ Emoji ở các trường định danh.
    /// </summary>
    public static string SanitizeText(string? input, string? fieldName = null, bool allowMultiline = false)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        // 1. Chuẩn hóa Unicode FormC (giữ nguyên tiếng Việt có dấu chuẩn NFC)
        var cleaned = NormalizeUnicodeSafe(input, FormKC: false);

        // 2. Xóa các ký tự full-width nguy hiểm (＜ ＞) bằng cách thay thế về ký tự thường trước khi lọc HTML
        cleaned = cleaned
            .Replace('\uFF1C', '<')
            .Replace('\uFF1E', '>');

        // 3. Xóa Null-byte, ký tự điều khiển ẩn, Zero-Width và BiDi Override
        cleaned = ControlAndInvisibleCharsRegex().Replace(cleaned, string.Empty);

        // 4. Chống Zalgo text làm vỡ giao diện
        cleaned = ZalgoCombiningMarksRegex().Replace(cleaned, string.Empty);

        // 5. Loại bỏ toàn bộ khối <script>...</script>, <style>...</style>, thẻ HTML và các mẫu sự kiện JS độc hại
        cleaned = DangerousPairedBlockRegex().Replace(cleaned, string.Empty);
        cleaned = HtmlTagRegex().Replace(cleaned, string.Empty);
        cleaned = DangerousJsPatternRegex().Replace(cleaned, string.Empty);

        // 6. Loại bỏ Emoji / Icon / Surrogate pair (Bắt buộc ở mọi trường trên hệ thống quản lý thư viện học thuật)
        cleaned = StripEmojiAndIcons(cleaned);

        // 7. Chuẩn hóa khoảng trắng
        if (!allowMultiline)
        {
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
        }
        else
        {
            cleaned = Regex.Replace(cleaned, @"[ \t]+", " ");
            cleaned = Regex.Replace(cleaned, @"(\r?\n){3,}", "\n\n");
        }

        return cleaned.Trim();
    }

    /// <summary>
    /// Kiểm tra chuỗi có chứa Emoji, Surrogate Pair hoặc Symbol/Pictograph hay không.
    /// </summary>
    public static bool ContainsEmojiOrSurrogate(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        foreach (var ch in input)
        {
            if (char.IsSurrogate(ch))
            {
                return true;
            }

            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.OtherSymbol || category == UnicodeCategory.PrivateUse)
            {
                return true;
            }
        }

        return EmojiAndIconRegex().IsMatch(input);
    }

    /// <summary>
    /// Loại bỏ triệt để Emoji, Surrogate Pairs và các ký hiệu Icon lạ.
    /// </summary>
    public static string StripEmojiAndIcons(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (char.IsSurrogate(ch))
            {
                continue;
            }

            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.OtherSymbol || category == UnicodeCategory.PrivateUse)
            {
                continue;
            }

            sb.Append(ch);
        }

        return EmojiAndIconRegex().Replace(sb.ToString(), string.Empty);
    }

    private static string NormalizeUnicodeSafe(string input, bool FormKC)
    {
        try
        {
            return input.Normalize(FormKC ? NormalizationForm.FormKC : NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            // Trường hợp chuỗi chứa unpaired surrogate không hợp lệ -> lọc bỏ surrogate lỗi
            var sb = new StringBuilder(input.Length);
            foreach (var ch in input)
            {
                if (!char.IsSurrogate(ch))
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }
    }

    // =========================================================================
    // KIỂM ĐỊNH AN NINH TỆP TẢI LÊN (MAGIC BYTES, DOUBLE EXTENSION, POLYGLOT, SVG XSS)
    // =========================================================================
    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jse", ".wsf", ".wsh",
        ".msc", ".scr", ".pif", ".com", ".hta", ".jar", ".war", ".php", ".phtml", ".php3",
        ".php4", ".php5", ".asp", ".aspx", ".ascx", ".ashx", ".asmx", ".cshtml", ".razor",
        ".config", ".html", ".htm", ".xhtml", ".shtml", ".sh", ".bash", ".py", ".rb", ".pl", ".cgi"
    };

    /// <summary>
    /// Kiểm tra toàn diện tệp hình ảnh tải lên:
    /// 1. Chống Path Traversal, Null-byte và Double Extension (VD: shell.aspx.jpg).
    /// 2. Kiểm tra Magic Bytes thực tế của luồng byte (JPEG, PNG, GIF, WEBP, SVG).
    /// 3. Quét chống Polyglot File (nhúng mã độc &lt;script&gt;, &lt;?php, &lt;%@, MZ header vào ảnh).
    /// 4. Kiểm tra an toàn cấu trúc XML nếu là tệp SVG (chặn &lt;script&gt;, event handler, XXE &lt;!ENTITY, foreignObject).
    /// </summary>
    public static async Task<(bool IsValid, string ErrorMessage)> ValidateUploadedImageAsync(
        IFormFile file,
        long maxBytes,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            return (false, "Tệp hình ảnh tải lên đang trống.");
        }

        if (file.Length > maxBytes)
        {
            return (false, $"Dung lượng ảnh \"{Path.GetFileName(file.FileName)}\" vượt quá giới hạn {maxBytes / (1024 * 1024)} MB.");
        }

        var rawFileName = file.FileName ?? string.Empty;
        if (rawFileName.Contains('\0') || rawFileName.Contains("..") || ControlAndInvisibleCharsRegex().IsMatch(rawFileName))
        {
            return (false, "Tên tệp tải lên chứa ký tự điều khiển hoặc đường dẫn không hợp lệ.");
        }

        var safeFileName = Path.GetFileName(rawFileName);
        var parts = safeFileName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return (false, "Tệp tải lên không có phần mở rộng hợp lệ.");
        }

        // Kiểm tra Double Extension (VD: exploit.aspx.png hoặc shell.php.jpg)
        for (var i = 1; i < parts.Length - 1; i++)
        {
            var middleExt = "." + parts[i];
            if (DangerousExtensions.Contains(middleExt))
            {
                return (false, $"Phát hiện tên tệp chứa phần mở rộng kép nguy hiểm ({middleExt}).");
            }
        }

        var ext = Path.GetExtension(safeFileName).ToLowerInvariant();
        var contentType = (file.ContentType ?? string.Empty).Trim().ToLowerInvariant();

        // Đọc nội dung tệp vào bộ nhớ đệm để kiểm tra Magic Bytes & Polyglot
        await using var stream = file.OpenReadStream();
        var buffer = new byte[Math.Min(file.Length, 256 * 1024)]; // Kiểm tra 256KB đầu tiên
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
        if (bytesRead < 8)
        {
            return (false, "Tệp hình ảnh quá nhỏ hoặc bị hỏng cấu trúc dữ liệu.");
        }

        // Chặn tệp thực thi Windows PE (MZ header: 0x4D 0x5A) hoặc ELF (0x7F 'E' 'L' 'F') giả danh ảnh
        if ((buffer[0] == 0x4D && buffer[1] == 0x5A) ||
            (buffer[0] == 0x7F && buffer[1] == 0x45 && buffer[2] == 0x4C && buffer[3] == 0x46))
        {
            return (false, "Phát hiện tệp thực thi nhị phân giả mạo định dạng hình ảnh.");
        }

        if (ext == ".svg")
        {
            if (contentType != "image/svg+xml")
            {
                return (false, "MIME Content-Type của tệp .svg không khớp (yêu cầu image/svg+xml).");
            }

            var svgText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            if (!svgText.Contains("<svg", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Tệp .svg không chứa cấu trúc thẻ <svg> hợp lệ.");
            }

            if (Regex.IsMatch(svgText, @"<\s*\/?\s*(script|iframe|object|embed|foreignObject)\b|<!ENTITY|<!DOCTYPE|\bon[a-z]+\s*=|javascript\s*:|vbscript\s*:|data\s*:\s*text\/html", RegexOptions.IgnoreCase))
            {
                return (false, "Phát hiện mã Script, XXE hoặc thuộc tính sự kiện độc hại bên trong tệp SVG.");
            }

            return (true, string.Empty);
        }

        // Kiểm tra Magic Bytes cho ảnh Raster (.jpg/.jpeg, .png, .gif, .webp)
        var isMagicValid = (ext, contentType) switch
        {
            (".jpg" or ".jpeg", "image/jpeg" or "image/pjpeg") =>
                buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF,

            (".png", "image/png") =>
                buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47 &&
                buffer[4] == 0x0D && buffer[5] == 0x0A && buffer[6] == 0x1A && buffer[7] == 0x0A,

            (".gif", "image/gif") =>
                bytesRead >= 6 &&
                buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x38 &&
                (buffer[4] == 0x37 || buffer[4] == 0x39) && buffer[5] == 0x61,

            (".webp", "image/webp") =>
                bytesRead >= 12 &&
                buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46 &&
                buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50,

            _ => false
        };

        if (!isMagicValid)
        {
            return (false, $"Chữ ký nhị phân (Magic Bytes) của tệp \"{safeFileName}\" không khớp với định dạng ảnh {ext}. Nghi ngờ tệp giả mạo.");
        }

        // Quét chống Polyglot File: ảnh hợp lệ nhưng bị chèn mã độc (<script, <?php, <%@, onerror=, javascript:) vào phần metadata/body
        var asciiScan = Encoding.Latin1.GetString(buffer, 0, bytesRead);
        if (Regex.IsMatch(asciiScan, @"<\s*script\b|<\?php|<%@\s*Page|\bon(error|load|click|mouseover)\s*=|javascript\s*:", RegexOptions.IgnoreCase))
        {
            return (false, $"Phát hiện mã thực thi (Polyglot Script Injection) nhúng bên trong tệp ảnh \"{safeFileName}\".");
        }

        return (true, string.Empty);
    }
}

