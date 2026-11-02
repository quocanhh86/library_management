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
            "Trang chủ",
            "/"),

        [ScreenId.Login] = new(
            ScreenId.Login,
            "SCR_LOGIN",
            "Đăng nhập",
            "/Account/Login"),

        [ScreenId.StaffLogin] = new(
            ScreenId.StaffLogin,
            "SCR_STAFF_LOGIN",
            "Đăng nhập cán bộ",
            "/Account/StaffLogin"),

        [ScreenId.Register] = new(
            ScreenId.Register,
            "SCR_REGISTER",
            "Đăng ký thẻ thư viện",
            "/Account/Register"),

        [ScreenId.VerifyOtp] = new(
            ScreenId.VerifyOtp,
            "SCR_VERIFY_OTP",
            "Nhập mã xác thực",
            "/Account/VerifyOtp"),

        [ScreenId.AccessDenied] = new(
            ScreenId.AccessDenied,
            "SCR_ACCESS_DENIED",
            "Không có quyền mở trang",
            "/Account/AccessDenied"),

        // 2. Cổng Độc giả
        [ScreenId.ReaderProfile] = new(
            ScreenId.ReaderProfile,
            "SCR_READER_PROFILE",
            "Thẻ thư viện của tôi",
            "/Reader/Profile",
            [AppRoles.Reader]),

        [ScreenId.ReaderBorrowHistory] = new(
            ScreenId.ReaderBorrowHistory,
            "SCR_READER_BORROW_HISTORY",
            "Lịch sử mượn",
            "/Reader/BorrowHistory",
            [AppRoles.Reader]),

        [ScreenId.ReaderFines] = new(
            ScreenId.ReaderFines,
            "SCR_READER_FINES",
            "Tiền phạt",
            "/Reader/Fines",
            [AppRoles.Reader]),

        [ScreenId.ReaderReservations] = new(
            ScreenId.ReaderReservations,
            "SCR_READER_RESERVATIONS",
            "Sách đặt trước",
            "/Reader/Reservations",
            [AppRoles.Reader]),

        [ScreenId.BookCatalog] = new(
            ScreenId.BookCatalog,
            "SCR_BOOK_CATALOG",
            "Tra cứu sách",
            "/Books"),

        [ScreenId.BookDetail] = new(
            ScreenId.BookDetail,
            "SCR_BOOK_DETAIL",
            "Chi tiết sách",
            "/Books/Detail/{id}"),

        // 3. Nghiệp vụ Mượn Trả
        [ScreenId.CirculationBorrow] = new(
            ScreenId.CirculationBorrow,
            "SCR_CIRCULATION_BORROW",
            "Lập phiếu mượn",
            "/Circulation/Borrow",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.CirculationReturn] = new(
            ScreenId.CirculationReturn,
            "SCR_CIRCULATION_RETURN",
            "Trả sách",
            "/Circulation/Return",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.CirculationHistory] = new(
            ScreenId.CirculationHistory,
            "SCR_CIRCULATION_HISTORY",
            "Phiếu mượn",
            "/Circulation/History",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageFines] = new(
            ScreenId.ManageFines,
            "SCR_MANAGE_FINES",
            "Thu phạt",
            "/Fines",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageReservations] = new(
            ScreenId.ManageReservations,
            "SCR_MANAGE_RESERVATIONS",
            "Đặt trước",
            "/Reservations",
            [AppRoles.Admin, AppRoles.Librarian]),

        // 4. Quản trị Kho sách & Danh mục
        [ScreenId.AdminDashboard] = new(
            ScreenId.AdminDashboard,
            "SCR_ADMIN_DASHBOARD",
            "Việc cần làm hôm nay",
            "/Admin/Dashboard",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageBooks] = new(
            ScreenId.ManageBooks,
            "SCR_MANAGE_BOOKS",
            "Đầu sách",
            "/Admin/Books",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageBookCopies] = new(
            ScreenId.ManageBookCopies,
            "SCR_MANAGE_BOOK_COPIES",
            "Bản sao",
            "/Admin/BookCopies",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageCategories] = new(
            ScreenId.ManageCategories,
            "SCR_MANAGE_CATEGORIES",
            "Thể loại",
            "/Admin/Categories",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageAuthors] = new(
            ScreenId.ManageAuthors,
            "SCR_MANAGE_AUTHORS",
            "Tác giả",
            "/Admin/Authors",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManagePublishers] = new(
            ScreenId.ManagePublishers,
            "SCR_MANAGE_PUBLISHERS",
            "Nhà xuất bản",
            "/Admin/Publishers",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageShelves] = new(
            ScreenId.ManageShelves,
            "SCR_MANAGE_SHELVES",
            "Kệ sách",
            "/Admin/Shelves",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageReaders] = new(
            ScreenId.ManageReaders,
            "SCR_MANAGE_READERS",
            "Độc giả",
            "/Admin/Readers",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.ManageReaderTypes] = new(
            ScreenId.ManageReaderTypes,
            "SCR_MANAGE_READER_TYPES",
            "Loại độc giả",
            "/Admin/ReaderTypes",
            [AppRoles.Admin]),

        // 5. Báo cáo & Cấu hình hệ thống
        [ScreenId.ReportsStatistics] = new(
            ScreenId.ReportsStatistics,
            "SCR_REPORTS_STATISTICS",
            "Báo cáo",
            "/Reports",
            [AppRoles.Admin, AppRoles.Librarian]),

        [ScreenId.SystemStaff] = new(
            ScreenId.SystemStaff,
            "SCR_SYSTEM_STAFF",
            "Người dùng",
            "/System/Staff",
            [AppRoles.Admin]),

        [ScreenId.SystemSettings] = new(
            ScreenId.SystemSettings,
            "SCR_SYSTEM_SETTINGS",
            "Quy định mượn trả",
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

