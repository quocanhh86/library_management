using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using web_mvc_management.Constant;
using web_mvc_management.Data;

namespace web_mvc_management.Infrastructure;

/// <summary>
/// Trình kiểm tra tính hợp lệ của phiên đăng nhập (SEC-05, SEC-21).
/// Tự động thu hồi phiên (Sign Out) ngay lập tức khi:
/// 1. Mật khẩu bị đổi (SecurityStamp trong DB thay đổi).
/// 2. Tài khoản bị vô hiệu hóa hoặc bị khóa (Status != ACTIVE).
/// 3. Bị xóa khỏi hệ thống.
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

        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();

        if (roleClaim == AppRoles.Reader)
        {
            var reader = await db.Readers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
            if (reader == null || reader.Status != EntityStatus.Active || reader.SecurityStamp != stampClaim || (reader.LockedUntil.HasValue && reader.LockedUntil.Value > DateTimeOffset.UtcNow))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }
        else if (roleClaim == AppRoles.Admin || roleClaim == AppRoles.Librarian)
        {
            var staff = await db.Staff.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
            if (staff == null || staff.Status != EntityStatus.Active || staff.SecurityStamp != stampClaim || (staff.LockedUntil.HasValue && staff.LockedUntil.Value > DateTimeOffset.UtcNow))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }
        else
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
