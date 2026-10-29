namespace web_mvc_management.Constant;

/// <summary>
/// Các vai trò người dùng trong hệ thống quản lý thư viện.
/// </summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Librarian = "Librarian";
    public const string Reader = "Reader";

    public static readonly string[] AllRoles = [Admin, Librarian, Reader];
}

