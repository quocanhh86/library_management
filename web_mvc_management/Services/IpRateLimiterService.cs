using System.Collections.Concurrent;
using System.Net;
using web_mvc_management.Infrastructure;

namespace web_mvc_management.Services;

/// <summary>
/// Triển khai dịch vụ giới hạn tần suất request theo IP (SEC-17: IP Rate Limiting).
/// Áp dụng thuật toán Sliding Window 60 giây an toàn đa luồng và chống giả mạo X-Forwarded-For từ client.
/// </summary>
public class IpRateLimiterService(IDateTimeProvider dateTimeProvider) : IIpRateLimiterService
{
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _requestTracker = new();
    private readonly Lock _lock = new();

    public string GetClientIp(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var remoteIp = httpContext.Connection.RemoteIpAddress;

        // Chỉ tin tưởng các header Proxy (CF-Connecting-IP, X-Forwarded-For, X-Real-IP)
        // khi kết nối vật lý đến từ Reverse Proxy nội bộ (Loopback), chống Client tự chèn header giả mạo để vượt Rate Limit
        if (remoteIp == null || IPAddress.IsLoopback(remoteIp))
        {
            var cfIp = httpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
            if (TryNormalizeIp(cfIp, out var validCfIp))
            {
                return validCfIp;
            }

            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                var firstIp = forwardedFor.Split(',')[0].Trim();
                if (TryNormalizeIp(firstIp, out var validForwardedIp))
                {
                    return validForwardedIp;
                }
            }

            var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (TryNormalizeIp(realIp, out var validRealIp))
            {
                return validRealIp;
            }
        }

        return remoteIp?.ToString() ?? "127.0.0.1";
    }

    private static bool TryNormalizeIp(string? rawIp, out string normalizedIp)
    {
        normalizedIp = string.Empty;
        if (string.IsNullOrWhiteSpace(rawIp))
        {
            return false;
        }

        var candidate = rawIp.Trim();
        if (IPAddress.TryParse(candidate, out var parsed))
        {
            normalizedIp = parsed.ToString();
            return true;
        }

        return false;
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
