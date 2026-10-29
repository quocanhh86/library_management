namespace web_mvc_management.Infrastructure;

/// <summary>
/// Giao diện cung cấp thời gian hệ thống, hỗ trợ viết Unit Test dễ dàng mock thời gian.
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset Now { get; }
    DateOnly Today { get; }
}

/// <summary>
/// Triển khai mặc định lấy thời gian thực từ hệ điều hành.
/// </summary>
public class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateTimeOffset Now => DateTimeOffset.Now;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}

