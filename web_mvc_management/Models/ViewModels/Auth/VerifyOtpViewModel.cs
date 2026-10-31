using System;
using System.ComponentModel.DataAnnotations;

namespace web_mvc_management.Models.ViewModels.Auth;

public class VerifyOtpViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? MaskedEmail { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã OTP")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP gồm đúng 6 chữ số")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP chỉ bao gồm 6 chữ số")]
    [Display(Name = "Mã xác thực OTP")]
    public string OtpCode { get; set; } = string.Empty;

    [Required]
    public string Purpose { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
