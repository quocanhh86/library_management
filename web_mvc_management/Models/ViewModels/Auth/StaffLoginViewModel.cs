using System;
using System.ComponentModel.DataAnnotations;

namespace web_mvc_management.Models.ViewModels.Auth;

public class StaffLoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ Gmail hoặc Email cơ quan")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không đúng định dạng")]
    [Display(Name = "Gmail / Email Cán Bộ")]
    public string Email { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
