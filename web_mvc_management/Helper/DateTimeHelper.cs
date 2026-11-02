using web_mvc_management.Constant;

namespace web_mvc_management.Helper;

/// <summary>
/// Hỗ trợ định dạng và hiển thị ngày tháng thân thiện với người dùng Việt Nam.
/// </summary>
public static class DateTimeHelper
{
    public static string ToVietnameseDateString(DateTimeOffset? dateTime) =>
        dateTime?.ToString(AppConstants.DefaultDateFormat) ?? "-";

    public static string ToVietnameseDateString(DateOnly? date) =>
        date?.ToString(AppConstants.DefaultDateFormat) ?? "-";

    public static string ToVietnameseDateTimeString(DateTimeOffset? dateTime) =>
        dateTime?.ToString(AppConstants.DefaultDateTimeFormat) ?? "-";

    /// <summary>
    /// Giờ:phút theo giờ Việt Nam, ví dụ "14:35". Dùng cho thông báo "thử lại lúc ...".
    /// </summary>
    // UTC+7 cố định (Việt Nam không có giờ mùa hè); dùng TimeZoneInfo nếu cần múi giờ khác
    public static string ToVietnamTimeString(DateTimeOffset dateTime) =>
        dateTime.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm");

    /// <summary>
    /// Hiển thị thời gian tương đối (ví dụ: "vừa xong", "10 phút trước", "3 ngày trước").
    /// </summary>
    public static string ToRelativeTime(DateTimeOffset dateTime)
    {
        var elapsed = DateTimeOffset.UtcNow - dateTime;

        if (elapsed.TotalSeconds < 60)
        {
            return "Vừa xong";
        }
        if (elapsed.TotalMinutes < 60)
        {
            var minutes = (int)elapsed.TotalMinutes;
            return $"{minutes} phút trước";
        }
        if (elapsed.TotalHours < 24)
        {
            var hours = (int)elapsed.TotalHours;
            return $"{hours} giờ trước";
        }
        if (elapsed.TotalDays < 30)
        {
            var days = (int)elapsed.TotalDays;
            return $"{days} ngày trước";
        }
        if (elapsed.TotalDays < 365)
        {
            var months = (int)(elapsed.TotalDays / 30);
            return $"{months} tháng trước";
        }

        var years = (int)(elapsed.TotalDays / 365);
        return $"{years} năm trước";
    }
}

