using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using web_mvc_management.Constant;
using web_mvc_management.Repositories;

namespace web_mvc_management.Infrastructure;

/// <summary>
/// Trình kiểm tra tính hợp lệ của phiên đăng nhập (SEC-05, SEC-21).
/// Tự động thu hồi phiên (Sign Out) ngay lập tức khi:
/// 1. Mật khẩu bị đổi (SecurityStamp trong DB thay đổi).
/// 2. Tài khoản bị vô hiệu hóa hoặc thẻ thư viện bị khóa (Status != ACTIVE hoặc CardStatus == LOCKED).
/// 3. Tài khoản bị khóa tạm thời do Brute-force hoặc bị xóa khỏi hệ thống.
/// </summary>
public static class SecurityStampValidator
{
    public const string SecurityStampClaimType = "SecurityStamp";

    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var user = context.Principal;
        if (user == null)
        {
            context.RejectPrincipal();
            return;
        }

        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value;
        var stampClaim = user.FindFirst(SecurityStampClaimType)?.Value;

        if (!Guid.TryParse(idClaim, out var userId) || string.IsNullOrWhiteSpace(roleClaim) || string.IsNullOrWhiteSpace(stampClaim))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        // Bỏ qua truy vấn DB lặp lại trong vòng 3 giây cho các request song song cùng một phiên
        var cache = context.HttpContext.RequestServices.GetService<IMemoryCache>();
        var cacheKey = $"sec_stamp_ok:{roleClaim}:{userId}:{stampClaim}";
        if (cache != null && cache.TryGetValue(cacheKey, out bool isValid) && isValid)
        {
            return;
        }

        var unitOfWork = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
        var dateTimeProvider = context.HttpContext.RequestServices.GetRequiredService<IDateTimeProvider>();
        var now = dateTimeProvider.UtcNow;

        if (roleClaim == AppRoles.Reader)
        {
            var reader = await unitOfWork.Readers.FirstOrDefaultAsync(x => x.Id == userId, asNoTracking: true);
            if (reader == null
                || reader.Status != EntityStatus.Active
                || reader.CardStatus == LibraryConstants.CardStatus.Locked
                || reader.SecurityStamp != stampClaim
                || (reader.LockedUntil.HasValue && reader.LockedUntil.Value > now))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }
        }
        else if (roleClaim == AppRoles.Admin || roleClaim == AppRoles.Librarian)
        {
            var staff = await unitOfWork.Staff.FirstOrDefaultAsync(x => x.Id == userId, asNoTracking: true);
            if (staff == null
                || staff.Status != EntityStatus.Active
                || staff.SecurityStamp != stampClaim
                || (staff.LockedUntil.HasValue && staff.LockedUntil.Value > now))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }
        }
        else
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        cache?.Set(cacheKey, true, TimeSpan.FromSeconds(3));
    }
}
