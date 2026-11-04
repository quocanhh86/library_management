using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using web_mvc_management.Constant;
using web_mvc_management.Services;
using web_mvc_management.Util;

namespace web_mvc_management.Middlewares;

/// <summary>
/// Middleware bảo vệ tầng HTTP Request/Response:
/// 1. Chống Cross-Site Request (Sec-Fetch-Site: cross-site, Origin/Referer mismatch) và CORS trái phép.
/// 2. Chặn Path Traversal (..), Null-byte (%00) trên URL và giới hạn HTTP Verb (chỉ cho phép GET, POST, HEAD, OPTIONS).
/// 3. Gắn đầy đủ HTTP Security Headers (CSP, X-Frame-Options, X-Content-Type-Options, COOP, Permissions-Policy).
/// 4. Đóng hộp cát (CSP Sandbox) đối với mọi tệp tĩnh trong thư mục /uploads/*.
/// </summary>
public class RequestSecurityMiddleware(RequestDelegate next, ILogger<RequestSecurityMiddleware> logger)
{
    private static readonly HashSet<string> AllowedHttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "HEAD", "OPTIONS"
    };

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // 1. Gắn bộ HTTP Security Headers chuẩn OWASP ASVS
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["X-Frame-Options"] = "DENY";
        response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
        response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
        response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

        // Nếu truy cập trực tiếp tệp trong /uploads/* -> Ép CSP Sandbox tuyệt đối không cho chạy script
        if (request.Path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase))
        {
            response.Headers["Content-Security-Policy"] = "default-src 'none'; img-src 'self' data:; style-src 'unsafe-inline'; sandbox";
        }
        else
        {
            response.Headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data: blob: https://res.cloudinary.com https://*.cloudinary.com; " +
                "font-src 'self' data:; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self';";
        }

        // 2. Chặn các HTTP Method lạ (PUT, DELETE, TRACE, PATCH, CONNECT...) vì hệ thống MVC chỉ dùng GET/POST
        if (!AllowedHttpMethods.Contains(request.Method))
        {
            response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        // 3. Chặn Null-byte hoặc Path Traversal trên URL Path / QueryString
        var rawPath = request.Path.Value ?? string.Empty;
        var rawQuery = request.QueryString.Value ?? string.Empty;
        if (rawPath.Contains('\0') || rawPath.Contains("..") ||
            rawQuery.Contains("%00", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[SECURITY BLOCK] Phát hiện URL/Query chứa Null-byte hoặc Path Traversal từ IP {Ip}: {Path}",
                context.Connection.RemoteIpAddress, rawPath);
            response.StatusCode = StatusCodes.Status400BadRequest;
            await response.WriteAsync("Yêu cầu không hợp lệ (Bad Request).");
            return;
        }

        // 4. Kiểm tra chặt CORS / Cross-Origin / Sec-Fetch-Site cho mọi request thay đổi dữ liệu (POST)
        if (HttpMethods.IsPost(request.Method))
        {
            var secFetchSite = request.Headers["Sec-Fetch-Site"].FirstOrDefault();
            if (string.Equals(secFetchSite, "cross-site", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("[SECURITY BLOCK] Chặn POST Cross-Site (Sec-Fetch-Site=cross-site) tới {Path}", rawPath);
                response.StatusCode = StatusCodes.Status403Forbidden;
                await response.WriteAsync("Từ chối yêu cầu Cross-Site (Forbidden).");
                return;
            }

            var origin = request.Headers.Origin.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(origin))
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri) ||
                    !string.Equals(originUri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("[SECURITY BLOCK] Chặn POST có Origin không khớp ({Origin} != {Host}) tới {Path}",
                        origin, request.Host.Value, rawPath);
                    response.StatusCode = StatusCodes.Status403Forbidden;
                    await response.WriteAsync("Origin không hợp lệ (CORS/CSRF Forbidden).");
                    return;
                }
            }
            else
            {
                var referer = request.Headers.Referer.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(referer) &&
                    Uri.TryCreate(referer, UriKind.Absolute, out var refererUri) &&
                    !string.Equals(refererUri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("[SECURITY BLOCK] Chặn POST có Referer ngoại lai ({Referer} != {Host}) tới {Path}",
                        referer, request.Host.Value, rawPath);
                    response.StatusCode = StatusCodes.Status403Forbidden;
                    await response.WriteAsync("Referer không hợp lệ (Forbidden).");
                    return;
                }
            }
        }

        await next(context);
    }
}

/// <summary>
/// Global Action Filter tự động làm sạch (Sanitize) và kiểm tra an ninh mọi tham số/ViewModel
/// trước khi vào bất kỳ Controller Action nào (chống sửa DOM/bỏ validate bằng F12 DevTools).
/// </summary>
public class InputSanitizationActionFilter(
    ITempDataDictionaryFactory tempDataFactory,
    ILogger<InputSanitizationActionFilter> logger) : IAsyncActionFilter
{
    private static readonly HashSet<string> PasswordFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password", "ConfirmPassword", "NewPassword", "CurrentPassword", "OldPassword"
    };

    private static readonly HashSet<string> MultilineFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Description", "Note", "Notes", "Summary", "Bio", "Address", "LockReason", "DisposeReason"
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var keys = context.ActionArguments.Keys.ToList();
        foreach (var key in keys)
        {
            var arg = context.ActionArguments[key];
            if (arg == null)
            {
                continue;
            }

            if (arg is string strVal)
            {
                if (!IsPasswordField(key, null))
                {
                    if (InputSanitizer.TryDetectMaliciousInput(strVal, key, out var reason))
                    {
                        AddSecurityError(context, key, reason);
                    }
                    context.ActionArguments[key] = InputSanitizer.SanitizeText(
                        strVal,
                        key,
                        MultilineFieldNames.Contains(key));
                }
            }
            else if (arg is IFormFile singleFile)
            {
                await ValidateFileArgumentAsync(context, key, singleFile);
            }
            else if (arg is IEnumerable<IFormFile> fileList)
            {
                foreach (var f in fileList)
                {
                    await ValidateFileArgumentAsync(context, key, f);
                }
            }
            else if (IsComplexViewModel(arg.GetType()))
            {
                await SanitizeObjectPropertiesAsync(context, arg);
            }
        }

        await next();
    }

    private async Task SanitizeObjectPropertiesAsync(ActionExecutingContext context, object target)
    {
        var type = target.GetType();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var val = prop.GetValue(target);
            if (val == null)
            {
                continue;
            }

            if (prop.PropertyType == typeof(string) && prop.CanWrite)
            {
                if (IsPasswordField(prop.Name, prop))
                {
                    continue;
                }

                var rawStr = (string)val;
                var displayName = prop.GetCustomAttribute<DisplayAttribute>()?.Name ?? prop.Name;

                if (InputSanitizer.TryDetectMaliciousInput(rawStr, prop.Name, out var reason))
                {
                    AddSecurityError(context, prop.Name, reason.Replace($"\"{prop.Name}\"", $"\"{displayName}\""));
                }

                var sanitized = InputSanitizer.SanitizeText(
                    rawStr,
                    prop.Name,
                    MultilineFieldNames.Contains(prop.Name));

                // Nếu thuộc tính có [StringLength] và bị F12 xóa maxlength trên trình duyệt -> Kiểm tra lại sau khi sanitize
                var strLenAttr = prop.GetCustomAttribute<StringLengthAttribute>();
                if (strLenAttr != null && sanitized.Length > strLenAttr.MaximumLength)
                {
                    AddSecurityError(context, prop.Name, $"Trường \"{displayName}\" vượt quá độ dài tối đa {strLenAttr.MaximumLength} ký tự.");
                }

                prop.SetValue(target, sanitized);
                if (context.ModelState.ContainsKey(prop.Name))
                {
                    context.ModelState.SetModelValue(prop.Name, new ValueProviderResult(sanitized));
                }
            }
            else if (val is IFormFile formFile)
            {
                await ValidateFileArgumentAsync(context, prop.Name, formFile);
            }
            else if (val is IEnumerable<IFormFile> formFiles)
            {
                foreach (var file in formFiles)
                {
                    await ValidateFileArgumentAsync(context, prop.Name, file);
                }
            }
            else if (val is IList<string> stringList)
            {
                for (var i = 0; i < stringList.Count; i++)
                {
                    var item = stringList[i];
                    if (string.IsNullOrEmpty(item)) continue;

                    if (InputSanitizer.TryDetectMaliciousInput(item, prop.Name, out var reason))
                    {
                        AddSecurityError(context, prop.Name, reason);
                    }
                    stringList[i] = InputSanitizer.SanitizeText(item, prop.Name);
                }
            }
            else if (IsComplexViewModel(prop.PropertyType) && val is not IEnumerable)
            {
                await SanitizeObjectPropertiesAsync(context, val);
            }
        }
    }

    private async Task ValidateFileArgumentAsync(ActionExecutingContext context, string fieldName, IFormFile file)
    {
        var (isValid, errorMsg) = await InputSanitizer.ValidateUploadedImageAsync(
            file,
            CloudinaryService.MaxFileSizeBytes,
            context.HttpContext.RequestAborted);

        if (!isValid)
        {
            AddSecurityError(context, fieldName, errorMsg);
        }
    }

    private void AddSecurityError(ActionExecutingContext context, string key, string message)
    {
        logger.LogWarning("[INPUT SECURITY] Phát hiện dữ liệu không an toàn tại trường '{Field}' trên {Path}: {Reason}",
            key, context.HttpContext.Request.Path, message);

        context.ModelState.AddModelError(key, message);
        context.ModelState.AddModelError(string.Empty, message);

        var tempData = tempDataFactory.GetTempData(context.HttpContext);
        tempData[AppConstants.TempDataKeys.ErrorMessage] = message;
    }

    private static bool IsPasswordField(string name, PropertyInfo? prop)
    {
        if (PasswordFieldNames.Contains(name) || name.Contains("Password", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var dataType = prop?.GetCustomAttribute<DataTypeAttribute>();
        return dataType?.DataType == DataType.Password;
    }

    private static bool IsComplexViewModel(Type type) =>
        type.IsClass &&
        type != typeof(string) &&
        !typeof(IFormFile).IsAssignableFrom(type) &&
        type.Namespace?.StartsWith("web_mvc_management", StringComparison.Ordinal) == true;
}
