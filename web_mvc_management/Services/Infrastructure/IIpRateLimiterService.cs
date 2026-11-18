namespace web_mvc_management.Services;

public interface IIpRateLimiterService
{
    string GetClientIp(HttpContext httpContext);

    bool IsAllowed(string ipAddress, string actionType, int maxRequestsPerMinute);

    void Reset(string ipAddress, string actionType);
}
