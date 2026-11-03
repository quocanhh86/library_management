using System.ComponentModel.DataAnnotations;

namespace web_mvc_management.Models.ViewModels.Auth;

public class VerifyOtpViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? MaskedEmail { get; set; }

    [Required(ErrorMessage = "Nhập đủ 6 số của mã xác thực")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã gồm đúng 6 số")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã chỉ gồm chữ số")]
    [Display(Name = "Mã xác thực")]
    public string OtpCode { get; set; } = string.Empty;

    [Required]
    public string Purpose { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
