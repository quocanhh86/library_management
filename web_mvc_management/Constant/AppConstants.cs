namespace web_mvc_management.Constant;

/// <summary>
/// Các hằng số hệ thống dùng chung trong ứng dụng.
/// </summary>
public static class AppConstants
{
    public const int DefaultPageIndex = 1;
    public const int DefaultPageSize = 10;
    public static readonly int[] AllowedPageSizes = [10, 20, 50, 100];

    public const string DefaultDateFormat = "dd/MM/yyyy";
    public const string DefaultDateTimeFormat = "dd/MM/yyyy HH:mm";

    /// <summary>
    /// Các khóa TempData dùng để truyền thông báo tới Razor Views.
    /// </summary>
    public static class TempDataKeys
    {
        public const string SuccessMessage = "SuccessMessage";
        public const string ErrorMessage = "ErrorMessage";
        public const string WarningMessage = "WarningMessage";
        public const string InfoMessage = "InfoMessage";
    }

    /// <summary>
    /// Schema Cookie Authentication.
    /// </summary>
    public static class Auth
    {
        public const string CookieScheme = "LibraryAuthCookie";
        public const string LoginPath = "/Account/Login";
        public const string LogoutPath = "/Account/Logout";
        public const string AccessDeniedPath = "/Account/AccessDenied";
    }
}

