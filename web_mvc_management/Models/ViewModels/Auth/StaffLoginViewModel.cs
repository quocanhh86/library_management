using System.ComponentModel.DataAnnotations;

namespace web_mvc_management.Models.ViewModels.Auth;

public class StaffLoginViewModel
{
    [Required(ErrorMessage = "Nhập email công vụ đã được cấp quyền")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không đúng định dạng")]
    [Display(Name = "Email công vụ")]
    public string Email { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
