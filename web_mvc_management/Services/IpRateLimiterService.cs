using System.Collections.Concurrent;
using web_mvc_management.Infrastructure;

namespace web_mvc_management.Services;

/// <summary>
/// Triển khai dịch vụ giới hạn tần suất request theo IP (SEC-17: IP Rate Limiting).
/// Áp dụng thuật toán Sliding Window 60 giây an toàn đa luồng.
/// </summary>
public class IpRateLimiterService(IDateTimeProvider dateTimeProvider) : IIpRateLimiterService
{
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _requestTracker = new();
    private readonly Lock _lock = new();

    public string GetClientIp(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // 1. Cloudflare header
        var cfIp = httpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cfIp))
        {
            return cfIp.Trim();
        }

        // 2. X-Forwarded-For (lấy IP đầu tiên của chuỗi client, proxy1, proxy2)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var firstIp = forwardedFor.Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(firstIp))
            {
                return firstIp;
            }
        }

        // 3. X-Real-IP
        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return realIp.Trim();
        }

        // 4. RemoteIpAddress fallback
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }

    public bool IsAllowed(string ipAddress, string actionType, int maxRequestsPerMinute)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || maxRequestsPerMinute <= 0)
        {
            return true;
        }

        var key = $"{ipAddress.Trim()}:{actionType.Trim().ToUpperInvariant()}";
        var now = dateTimeProvider.UtcNow;
        var oneMinuteAgo = now.AddMinutes(-1);

        lock (_lock)
        {
            var timestamps = _requestTracker.GetOrAdd(key, _ => []);

            // Dọn dẹp các mốc thời gian ngoài cửa sổ trượt 60 giây
            timestamps.RemoveAll(t => t < oneMinuteAgo);

            if (timestamps.Count >= maxRequestsPerMinute)
            {
                return false;
            }

            timestamps.Add(now);
            return true;
        }
    }

    public void Reset(string ipAddress, string actionType)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return;
        }

        var key = $"{ipAddress.Trim()}:{actionType.Trim().ToUpperInvariant()}";
        _requestTracker.TryRemove(key, out _);
    }
}
