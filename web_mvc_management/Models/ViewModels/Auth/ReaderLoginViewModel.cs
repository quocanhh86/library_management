using System;
using System.ComponentModel.DataAnnotations;

namespace web_mvc_management.Models.ViewModels.Auth;

public class ReaderLoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email")]
    [Display(Name = "Tên đăng nhập hoặc Email")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; } = false;

    public string? ReturnUrl { get; set; }
}
