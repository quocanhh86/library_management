using System.ComponentModel.DataAnnotations;
using web_mvc_management.Common;
using web_mvc_management.Constant;

namespace web_mvc_management.Models.ViewModels.SystemAdmin;

// =============================================================================
// 1. QUẢN LÝ TÀI KHOẢN CÁN BỘ & PHÂN QUYỀN (/System/Staff)
// =============================================================================
public class StaffFilter : BaseFilter
{
    public string? Role { get; set; }
}

public class StaffListItemViewModel
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string Role { get; init; } = AppRoles.Librarian;
    public string Status { get; init; } = EntityStatus.Active;
    public DateTimeOffset? LastLoginAt { get; init; }
    public DateTimeOffset? LockedUntil { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public class StaffCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập từ 3 đến 50 ký tự.")]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ email công vụ / Gmail.")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
    [StringLength(255)]
    [Display(Name = "Email (nhận mã OTP)")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ và tên cán bộ.")]
    [StringLength(150)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn vai trò phân quyền.")]
    [Display(Name = "Vai trò hệ thống")]
    public string Role { get; set; } = AppRoles.Librarian;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu khởi tạo.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu ít nhất 6 ký tự.")]
    [Display(Name = "Mật khẩu khởi tạo")]
    public string Password { get; set; } = string.Empty;
}

public class StaffEditViewModel
{
    [Required]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ và tên cán bộ.")]
    [StringLength(150)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(255)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn vai trò phân quyền.")]
    [Display(Name = "Vai trò hệ thống")]
    public string Role { get; set; } = AppRoles.Librarian;

    [Required]
    [Display(Name = "Trạng thái tài khoản")]
    public string Status { get; set; } = EntityStatus.Active;

    [StringLength(100)]
    [Display(Name = "Đặt lại mật khẩu mới (để trống nếu giữ nguyên)")]
    public string? NewPassword { get; set; }
}

public class StaffPageViewModel
{
    public PagedResult<StaffListItemViewModel> PagedStaff { get; init; } = PagedResult<StaffListItemViewModel>.Empty();
    public StaffFilter Filter { get; init; } = new();
    public StaffCreateViewModel CreateForm { get; init; } = new();
    public Guid? CurrentUserId { get; init; }
}

// =============================================================================
// 2. CẤU HÌNH LOẠI ĐỘC GIẢ & QUY ĐỊNH MƯỢN TRẢ (/Admin/ReaderTypes & /System/Settings)
// =============================================================================
public class ReaderTypeItemViewModel
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public short MaxBorrowBooks { get; init; }
    public short LoanDays { get; init; }
    public short MaxRenewals { get; init; }
    public short RenewDays { get; init; }
    public short CardValidMonths { get; init; }
    public string? Description { get; init; }
    public string Status { get; init; } = EntityStatus.Active;
    public int ReaderCount { get; init; }
}

public class ReaderTypeFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã loại độc giả (ví dụ: STUDENT, LECTURER).")]
    [StringLength(20, ErrorMessage = "Mã loại không vượt quá 20 ký tự.")]
    [Display(Name = "Mã loại độc giả")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên loại độc giả.")]
    [StringLength(100, ErrorMessage = "Tên loại không vượt quá 100 ký tự.")]
    [Display(Name = "Tên loại độc giả")]
    public string Name { get; set; } = string.Empty;

    [Range(1, 50, ErrorMessage = "Số sách mượn tối đa phải từ 1 đến 50 cuốn.")]
    [Display(Name = "Số sách mượn tối đa (cuốn)")]
    public short MaxBorrowBooks { get; set; } = 5;

    [Range(1, 180, ErrorMessage = "Số ngày mượn phải từ 1 đến 180 ngày.")]
    [Display(Name = "Số ngày mượn tối đa (ngày)")]
    public short LoanDays { get; set; } = 14;

    [Range(0, 10, ErrorMessage = "Số lần gia hạn phải từ 0 đến 10 lần.")]
    [Display(Name = "Số lần gia hạn tối đa")]
    public short MaxRenewals { get; set; } = 1;

    [Range(0, 60, ErrorMessage = "Số ngày mỗi lần gia hạn phải từ 0 đến 60 ngày.")]
    [Display(Name = "Số ngày mỗi lần gia hạn")]
    public short RenewDays { get; set; } = 7;

    [Range(1, 120, ErrorMessage = "Thời hạn thẻ phải từ 1 đến 120 tháng.")]
    [Display(Name = "Thời hạn thẻ (tháng)")]
    public short CardValidMonths { get; set; } = 48;

    [StringLength(255)]
    [Display(Name = "Mô tả đối tượng")]
    public string? Description { get; set; }

    [Required]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = EntityStatus.Active;
}

public class ReaderTypesPageViewModel
{
    public IReadOnlyList<ReaderTypeItemViewModel> Items { get; init; } = [];
    public ReaderTypeFormViewModel Form { get; init; } = new();
}

/// <summary>
/// Cấu hình quy định mức phạt và vận hành thư viện (bảng library_settings).
/// </summary>
public class LibrarySettingFormViewModel
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mức phạt quá hạn mỗi ngày.")]
    [Range(0, 1000000, ErrorMessage = "Mức phạt mỗi ngày phải từ 0 đến 1.000.000 VNĐ.")]
    [Display(Name = "Mức phạt quá hạn mỗi ngày (VNĐ / cuốn / ngày)")]
    public decimal OverdueFinePerDay { get; set; } = 2000m;

    [Required(ErrorMessage = "Vui lòng nhập tỷ lệ phạt làm hỏng sách.")]
    [Range(0, 500, ErrorMessage = "Tỷ lệ phạt hỏng sách từ 0% đến 500% giá bìa.")]
    [Display(Name = "Tỷ lệ đền bù làm hỏng sách (% giá bìa)")]
    public short DamagedFinePercent { get; set; } = 50;

    [Required(ErrorMessage = "Vui lòng nhập tỷ lệ đền bù làm mất sách.")]
    [Range(50, 500, ErrorMessage = "Tỷ lệ đền bù mất sách từ 50% đến 500% giá bìa.")]
    [Display(Name = "Tỷ lệ đền bù làm mất sách (% giá bìa)")]
    public short LostFinePercent { get; set; } = 150;

    [Required(ErrorMessage = "Vui lòng nhập mức nợ phạt tối đa.")]
    [Range(0, 10000000, ErrorMessage = "Mức nợ phạt tối đa từ 0 đến 10.000.000 VNĐ.")]
    [Display(Name = "Trần nợ phạt tối đa trước khi khóa quyền mượn (VNĐ)")]
    public decimal MaxUnpaidFine { get; set; } = 50000m;

    [Required(ErrorMessage = "Vui lòng nhập thời gian giữ sách đặt trước.")]
    [Range(1, 336, ErrorMessage = "Thời gian giữ chỗ đặt trước từ 1 đến 336 giờ.")]
    [Display(Name = "Thời gian giữ sách đặt trước (giờ)")]
    public short ReservationHoldHours { get; set; } = 48;

    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// Trang cấu hình quy định tổng hợp (/System/Settings):
/// Bao gồm cả Mức phạt (LibrarySetting) và Số ngày mượn / Số sách tối đa theo Loại độc giả (ReaderTypes).
/// </summary>
public class SystemSettingsPageViewModel
{
    public LibrarySettingFormViewModel SettingForm { get; init; } = new();
    public IReadOnlyList<ReaderTypeItemViewModel> ReaderTypes { get; init; } = [];
}

