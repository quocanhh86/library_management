using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace web_mvc_management.Common;

/// <summary>
/// Validation attribute kiểm tra chuỗi họ tên / tên danh mục không được chứa chữ số hoặc ký tự đặc biệt nguy hiểm (SEC-34).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed partial class NoNumbersAttribute : ValidationAttribute
{
    public bool AllowAmpersandAndSlash { get; set; }

    public NoNumbersAttribute()
    {
        ErrorMessage = "Trường này chỉ được chứa chữ cái, khoảng trắng và dấu câu cơ bản (không được chứa chữ số hoặc ký tự đặc biệt).";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is not string str || string.IsNullOrWhiteSpace(str))
        {
            return ValidationResult.Success;
        }

        var trimmed = str.Trim();
        var pattern = AllowAmpersandAndSlash
            ? AllowedCategoryNameRegex()
            : AllowedPersonNameRegex();

        if (!pattern.IsMatch(trimmed))
        {
            var displayName = validationContext.DisplayName ?? validationContext.MemberName ?? "Trường dữ liệu";
            var msg = string.IsNullOrWhiteSpace(ErrorMessage)
                ? $"{displayName} không được chứa chữ số hoặc ký tự đặc biệt."
                : ErrorMessage;
            return new ValidationResult(msg, validationContext.MemberName is not null ? [validationContext.MemberName] : null);
        }

        return ValidationResult.Success;
    }

    // Cho phép chữ cái Unicode (bao gồm tiếng Việt có dấu), khoảng trắng, dấu chấm, dấu phẩy, gạch ngang, nháy đơn và ngoặc đơn
    [GeneratedRegex(@"^[\p{L}\p{M}\s.,\-'()]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex AllowedPersonNameRegex();

    // Cho phép thêm dấu &, / cho tên Thể loại sách (ví dụ: Khoa học & Công nghệ, AI / Học máy)
    [GeneratedRegex(@"^[\p{L}\p{M}\s.,\-'()&/]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex AllowedCategoryNameRegex();
}
