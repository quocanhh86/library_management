using System.ComponentModel.DataAnnotations;
using web_mvc_management.Common;

namespace web_mvc_management.Models.ViewModels.Auth;

/// <summary>
/// ViewModel hiển thị tùy chọn loại độc giả trên dropdown đăng ký (Strict ViewModel - không lộ Entity ra View).
/// </summary>
public class ReaderTypeOptionViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int MaxBorrowBooks { get; set; }
    public int LoanDays { get; set; }
}

/// <summary>
/// Kết quả trả về sau khi hoàn tất đăng ký mở thẻ độc giả.
/// </summary>
public class RegisteredReaderViewModel
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string CardNo { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel đăng ký tài khoản độc giả (SEC-18: Strict DTO Mass Assignment Defense).
/// </summary>
public class ReaderRegisterViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
    [StringLength(150, ErrorMessage = "Họ tên không được vượt quá 150 ký tự")]
    [NoNumbers(ErrorMessage = "Họ và tên không được chứa chữ số hoặc ký tự đặc biệt")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
    [StringLength(50, MinimumLength = 4, ErrorMessage = "Tên đăng nhập phải từ 4 đến 50 ký tự")]
    [RegularExpression(@"^[a-zA-Z0-9_.]+$", ErrorMessage = "Tên đăng nhập chỉ chứa chữ cái, số, dấu chấm hoặc gạch dưới")]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ email")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không đúng định dạng")]
    [RegularExpression(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", ErrorMessage = "Địa chỉ email không đúng định dạng (ví dụ: sinhvien@student.utc.edu.vn)")]
    [StringLength(255, ErrorMessage = "Email không được vượt quá 255 ký tự")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [Phone(ErrorMessage = "Số điện thoại không đúng định dạng")]
    [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Số điện thoại Việt Nam không hợp lệ (ví dụ: 0912345678)")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn loại độc giả")]
    [Display(Name = "Loại độc giả")]
    public Guid ReaderTypeId { get; set; }

    [StringLength(30, MinimumLength = 3, ErrorMessage = "Mã sinh viên / cán bộ phải từ 3 đến 30 ký tự")]
    [RegularExpression(@"^[A-Za-z0-9._\-]{3,30}$", ErrorMessage = "Mã sinh viên / cán bộ chỉ chứa chữ cái không dấu, chữ số, dấu gạch ngang hoặc gạch dưới")]
    [Display(Name = "Mã sinh viên (nếu có)")]
    public string? MemberCode { get; set; }

    [Display(Name = "Ngày sinh")]
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(255, ErrorMessage = "Địa chỉ không vượt quá 255 ký tự")]
    [Display(Name = "Địa chỉ")]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải từ 8 ký tự trở lên")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
        ErrorMessage = "Mật khẩu phải chứa ít nhất 1 chữ hoa, 1 chữ thường, 1 số và 1 ký tự đặc biệt")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận lại mật khẩu")]
    [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    [DataType(DataType.Password)]
    [Display(Name = "Xác nhận mật khẩu")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DateOfBirth.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (DateOfBirth.Value < new DateOnly(1900, 1, 1))
            {
                yield return new ValidationResult(
                    "Ngày sinh không hợp lệ (phải từ năm 1900 trở đi).",
                    [nameof(DateOfBirth)]);
            }
            else if (DateOfBirth.Value > today.AddYears(-6))
            {
                yield return new ValidationResult(
                    "Độc giả đăng ký thẻ phải từ 6 tuổi trở lên (ngày sinh không hợp lệ).",
                    [nameof(DateOfBirth)]);
            }
        }
    }
}
