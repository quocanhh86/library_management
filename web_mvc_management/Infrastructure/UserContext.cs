using System.Security.Claims;

namespace web_mvc_management.Infrastructure;

/// <summary>
/// Trích xuất danh tính người dùng hiện tại từ HttpContext Claims.
/// </summary>
public class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? CurrentUserId
    {
        get
        {
            var idClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idClaim, out var guid) ? guid : null;
        }
    }

    public string? CurrentUsername => User?.FindFirst(ClaimTypes.Name)?.Value;

    public string? CurrentUserRole => User?.FindFirst(ClaimTypes.Role)?.Value;

    public string? CurrentUserEmail => User?.FindFirst(ClaimTypes.Email)?.Value;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}

