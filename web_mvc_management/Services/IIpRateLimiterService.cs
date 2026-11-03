namespace web_mvc_management.Services;

public interface IIpRateLimiterService
{
    /// <summary>
    /// Trích xuất địa chỉ Real-IP của client an toàn phía sau Nginx/Cloudflare reverse proxy.
    /// </summary>
    string GetClientIp(HttpContext httpContext);

    /// <summary>
    /// Kiểm tra và ghi nhận lượt request theo IP (SEC-17 Sliding Window Rate Limiting).
    /// </summary>
    bool IsAllowed(string ipAddress, string actionType, int maxRequestsPerMinute);

    /// <summary>
    /// Reset bộ đếm request của một IP (ví dụ khi verify thành công).
    /// </summary>
    void Reset(string ipAddress, string actionType);
}
