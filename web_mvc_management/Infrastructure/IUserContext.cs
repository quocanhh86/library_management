namespace web_mvc_management.Infrastructure;

/// <summary>
/// Cung cấp thông tin người dùng hiện tại đang đăng nhập từ HttpContext.
/// </summary>
public interface IUserContext
{
    Guid? CurrentUserId { get; }
    string? CurrentUsername { get; }
    string? CurrentUserRole { get; }
    string? CurrentUserEmail { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}

