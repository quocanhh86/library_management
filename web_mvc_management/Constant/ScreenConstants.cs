using System;
using System.Collections.Generic;

namespace web_mvc_management.Constant;

/// <summary>
/// Định danh duy nhất (Enum) cho từng màn hình trong toàn bộ hệ thống Thư Viện UTC.
/// </summary>
public enum ScreenId
{
    // =========================================================================
    // 1. CỔNG KHÁCH & XÁC THỰC (PUBLIC & AUTHENTICATION)
    // =========================================================================
    Home,
    Login,
    StaffLogin,
    Register,
    VerifyOtp,
    AccessDenied,

    // =========================================================================
    // 2. CỔNG ĐỘC GIẢ (READER PORTAL)
    // =========================================================================
    ReaderProfile,
    ReaderBorrowHistory,
    ReaderFines,
    ReaderReservations,
    BookCatalog,
    BookDetail,

    // =========================================================================
    // 3. CỔNG NGHIỆP VỤ THỦ THƯ (CIRCULATION & LIBRARY OPERATIONS)
    // =========================================================================
    CirculationBorrow,
    CirculationReturn,
    CirculationHistory,
    ManageFines,
    ManageReservations,

    // =========================================================================
    // 4. QUẢN TRỊ KHO SÁCH & HỒ SƠ (CATALOG & MASTER DATA MANAGEMENT)
    // =========================================================================
    AdminDashboard,
    ManageBooks,
    ManageBookCopies,
    ManageCategories,
    ManageAuthors,
    ManagePublishers,
    ManageShelves,
    ManageReaders,
    ManageReaderTypes,

    // =========================================================================
    // 5. BÁO CÁO & HỆ THỐNG (REPORTS & SYSTEM CONFIGURATION)
    // =========================================================================
    ReportsStatistics,
    SystemStaff,
    SystemSettings
}

/// <summary>
/// Mô tả chi tiết thông tin màn hình bao gồm ScreenId, Mã màn hình, Tên tiếng Việt, Đường dẫn URL (Path) và Vai trò cho phép.
/// </summary>
public record ScreenInfo(
    ScreenId ScreenId,
    string Code,
    string Name,
    string Path,
    string[]? AllowedRoles = null);

/// <summary>
/// Danh bạ tra cứu toàn bộ màn hình hệ thống theo ScreenId, Code hoặc Path.
/// </summary>
public static class Screens
{
    public static readonly IReadOnlyDictionary<ScreenId, ScreenInfo> All = new Dictionary<ScreenId, ScreenInfo>
    {
        // 1. Cổng Khách & Xác thực
        [ScreenId.Home] = new(
            ScreenId.Home,
            "SCR_HOME",
            "Trang Chủ Thư Viện",
            "/"),

        [ScreenId.Login] = new(
            ScreenId.Login,
            "SCR_LOGIN",
            "Đăng Nhập Độc Giả",
            "/Account/Login"),

        [ScreenId.StaffLogin] = new(
            ScreenId.StaffLogin,
            "SCR_STAFF_LOGIN",
            "Đăng Nhập Cán Bộ & Quản Trị Viên",
            "/Account/StaffLogin"),

        [ScreenId.Register] = new(
            ScreenId.Register,
            "SCR_REGISTER",
            "Đăng Ký Mở Thẻ Bạn Đọc",
            "/Account/Register"),

        [ScreenId.VerifyOtp] = new(
            ScreenId.VerifyOtp,
            "SCR_VERIFY_OTP",
            "Xác Thực Mã OTP Bảo Mật",
            "/Account/VerifyOtp"),

        [ScreenId.AccessDenied] = new(
            ScreenId.AccessDenied,
            "SCR_ACCESS_DENIED",
            "Từ Chối Truy Cập (403)",
            "/Account/AccessDenied"),

        // 2. Cổng Độc giả
        [ScreenId.ReaderProfile] = new(
            ScreenId.ReaderProfile,
            "SCR_READER_PROFILE",
            "Hồ Sơ & Thẻ Thư Viện Cá Nhân",
            "/Reader/Profile",
            [AppRoles.Reader]),

        [ScreenId.ReaderBorrowHistory] = new(
            ScreenId.ReaderBorrowHistory,
            "SCR_READER_BORROW_HISTORY",
            "Lịch Sử Mượn Trả Sách",
            "/Reader/BorrowHistory",
            [AppRoles.Reader]),

        [ScreenId.ReaderFines] = new(
            ScreenId.ReaderFines,
            "SCR_READER_FINES",
            "Tra Cứu Tiền Phạt",
            "/Reader/Fines",
            [AppRoles.Reader]),

        [ScreenId.ReaderReservations] = new(
            ScreenId.ReaderReservations,
            "SCR_READER_RESERVATIONS",
            "Danh Sách Sách Đặt Trước",
            "/Reader/Reservations",
            [AppRoles.Reader]),

        [ScreenId.BookCatalog] = new(
            ScreenId.BookCatalog,
            "SCR_BOOK_CATALOG",
            "Tra Cứu Danh Mục Sách",
            "/Books"),

        [ScreenId.BookDetail] = new(
            ScreenId.BookDetail,
            "SCR_BOOK_DETAIL",
            "Chi Tiết Đầu Sách & Bản Sao",
            "/Books/Detail/{id}"),

        // 3. Nghiệp vụ Mượn Trả
        [ScreenId.CirculationBorrow] = new(
            ScreenId.CirculationBorrow,
            "SCR_CIRCULATION_BORROW",
            "Lập Phiếu Mượn Sách Mới",
            "/Circulation/Borrow",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.CirculationReturn] = new(
            ScreenId.CirculationReturn,
            "SCR_CIRCULATION_RETURN",
            "Ghi Nhận Trả Sách & Xử Lý Vi Phạm",
            "/Circulation/Return",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.CirculationHistory] = new(
            ScreenId.CirculationHistory,
            "SCR_CIRCULATION_HISTORY",
            "Quản Lý Lịch Sử Mượn Trả",
            "/Circulation/History",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageFines] = new(
            ScreenId.ManageFines,
            "SCR_MANAGE_FINES",
            "Xử Lý & Thu Tiền Phạt",
            "/Fines",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageReservations] = new(
            ScreenId.ManageReservations,
            "SCR_MANAGE_RESERVATIONS",
            "Quản Lý Đặt Giữ Sách",
            "/Reservations",
            [AppRoles.Admin, AppRoles.Librarian]),

        // 4. Quản trị Kho sách & Danh mục
        [ScreenId.AdminDashboard] = new(
            ScreenId.AdminDashboard,
            "SCR_ADMIN_DASHBOARD",
            "Bảng Điều Khiển Tổng Quan",
            "/Admin/Dashboard",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageBooks] = new(
            ScreenId.ManageBooks,
            "SCR_MANAGE_BOOKS",
            "Quản Lý Đầu Sách",
            "/Admin/Books",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageBookCopies] = new(
            ScreenId.ManageBookCopies,
            "SCR_MANAGE_BOOK_COPIES",
            "Quản Lý Cuốn Sách (Bản Sao / Mã Vạch)",
            "/Admin/BookCopies",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageCategories] = new(
            ScreenId.ManageCategories,
            "SCR_MANAGE_CATEGORIES",
            "Quản Lý Thể Loại Sách",
            "/Admin/Categories",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageAuthors] = new(
            ScreenId.ManageAuthors,
            "SCR_MANAGE_AUTHORS",
            "Quản Lý Tác Giả",
            "/Admin/Authors",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManagePublishers] = new(
            ScreenId.ManagePublishers,
            "SCR_MANAGE_PUBLISHERS",
            "Quản Lý Nhà Xuất Bản",
            "/Admin/Publishers",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageShelves] = new(
            ScreenId.ManageShelves,
            "SCR_MANAGE_SHELVES",
            "Quản Lý Vị Trí Giá / Kệ Sách",
            "/Admin/Shelves",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageReaders] = new(
            ScreenId.ManageReaders,
            "SCR_MANAGE_READERS",
            "Quản Lý Hồ Sơ & Thẻ Độc Giả",
            "/Admin/Readers",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageReaderTypes] = new(
            ScreenId.ManageReaderTypes,
            "SCR_MANAGE_READER_TYPES",
            "Quản Lý Nhóm & Hạn Mức Loại Độc Giả",
            "/Admin/ReaderTypes",
            [AppRoles.Admin]),

        // 5. Báo cáo & Cấu hình hệ thống
        [ScreenId.ReportsStatistics] = new(
            ScreenId.ReportsStatistics,
            "SCR_REPORTS_STATISTICS",
            "Báo Cáo Thống Kê & Phân Tích Thư Viện",
            "/Reports",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.SystemStaff] = new(
            ScreenId.SystemStaff,
            "SCR_SYSTEM_STAFF",
            "Quản Lý Tài Khoản Cán Bộ & Phân Quyền",
            "/System/Staff",
            [AppRoles.Admin]),

        [ScreenId.SystemSettings] = new(
            ScreenId.SystemSettings,
            "SCR_SYSTEM_SETTINGS",
            "Cấu Hình Thông Số & Quy Định Thư Viện",
            "/System/Settings",
            [AppRoles.Admin])
    };

    /// <summary>
    /// Lấy thông tin đầy đủ của màn hình theo ScreenId.
    /// </summary>
    public static ScreenInfo Get(ScreenId id) => All[id];

    /// <summary>
    /// Lấy URL Path của màn hình theo ScreenId.
    /// </summary>
    public static string GetPath(ScreenId id) => All[id].Path;

    /// <summary>
    /// Lấy tên tiếng Việt của màn hình theo ScreenId.
    /// </summary>
    public static string GetName(ScreenId id) => All[id].Name;

    /// <summary>
    /// Lấy mã Code của màn hình theo ScreenId (ví dụ SCR_LOGIN).
    /// </summary>
    public static string GetCode(ScreenId id) => All[id].Code;
}

