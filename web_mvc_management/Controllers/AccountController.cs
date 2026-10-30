using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Data;
using web_mvc_management.Models.ViewModels.Auth;
using web_mvc_management.Services;
using web_mvc_management.Util;

namespace web_mvc_management.Controllers;

public class AccountController(
    IAuthService authService,
    IIpRateLimiterService rateLimiter,
    ApplicationDbContext dbContext) : BaseController
{
    private string ClientIp => rateLimiter.GetClientIp(HttpContext);

    // =========================================================================
    // 1. ĐĂNG KÝ ĐỘC GIẢ (READER REGISTER)
    // =========================================================================
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewBag.ReaderTypes = await dbContext.ReaderTypes
            .Where(x => x.Status == EntityStatus.Active)
            .OrderBy(x => x.Name)
            .ToListAsync();

        return View(new ReaderRegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendRegisterOtp([FromBody] ReaderRegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return AjaxError("Dữ liệu đăng ký không hợp lệ.", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        }

        var result = await authService.SendRegisterOtpAsync(model, ClientIp);
        if (!result.Succeeded)
        {
            return AjaxError(result.Message, result.Errors);
        }

        return AjaxSuccess(result.Message, new
        {
            email = model.Email,
            maskedEmail = StringUtil.MaskEmail(model.Email),
            purpose = AuthService.PurposeRegister
        });
    }

    public class CompleteRegistrationRequest
    {
        public ReaderRegisterViewModel RegistrationData { get; set; } = null!;
        public string OtpCode { get; set; } = string.Empty;
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteRegistration([FromBody] CompleteRegistrationRequest request)
    {
        if (request?.RegistrationData == null || string.IsNullOrWhiteSpace(request.OtpCode))
        {
            return AjaxError("Thông tin xác thực không đầy đủ.");
        }

        var result = await authService.CompleteRegistrationAsync(request.RegistrationData, request.OtpCode, ClientIp);
        if (!result.Succeeded)
        {
            return AjaxError(result.Message, result.Errors);
        }

        SetSuccessMessage("Đăng ký thành công! Bạn có thể đăng nhập ngay bây giờ.");
        return AjaxSuccess(result.Message, new
        {
            redirectUrl = Url.Action(nameof(Login)),
            cardNo = result.Data?.CardNo
        });
    }

    // =========================================================================
    // 2. ĐĂNG NHẬP ĐỘC GIẢ (READER LOGIN 2FA)
    // =========================================================================
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new ReaderLoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(ReaderLoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest)
            {
                return AjaxError("Vui lòng điền đầy đủ tên đăng nhập và mật khẩu.");
            }
            return View(model);
        }

        var result = await authService.InitiateReaderLoginAsync(model, ClientIp);
        if (!result.Succeeded)
        {
            if (IsAjaxRequest)
            {
                return AjaxError(result.Message, result.Errors);
            }
            SetErrorMessage(result.Message);
            return View(model);
        }

        if (IsAjaxRequest)
        {
            return AjaxSuccess(result.Message, new
            {
                requiresOtp = true,
                email = result.Data!.Email,
                maskedEmail = result.Data.MaskedEmail,
                purpose = result.Data.Purpose,
                returnUrl = result.Data.ReturnUrl
            });
        }

        return RedirectToAction(nameof(VerifyOtp), new
        {
            email = result.Data!.Email,
            purpose = result.Data.Purpose,
            returnUrl = result.Data.ReturnUrl
        });
    }

    // =========================================================================
    // 3. ĐĂNG NHẬP CÁN BỘ / THỦ THƯ (STAFF PASSWORDLESS GMAIL OTP)
    // =========================================================================
    [HttpGet]
    [AllowAnonymous]
    public IActionResult StaffLogin(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new StaffLoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StaffLogin(StaffLoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest)
            {
                return AjaxError("Địa chỉ email không đúng định dạng.");
            }
            return View(model);
        }

        var result = await authService.InitiateStaffLoginAsync(model, ClientIp);
        if (!result.Succeeded)
        {
            if (IsAjaxRequest)
            {
                return AjaxError(result.Message, result.Errors);
            }
            SetErrorMessage(result.Message);
            return View(model);
        }

        if (IsAjaxRequest)
        {
            return AjaxSuccess(result.Message, new
            {
                requiresOtp = true,
                email = result.Data!.Email,
                maskedEmail = result.Data.MaskedEmail,
                purpose = result.Data.Purpose,
                returnUrl = result.Data.ReturnUrl
            });
        }

        return RedirectToAction(nameof(VerifyOtp), new
        {
            email = result.Data!.Email,
            purpose = result.Data.Purpose,
            returnUrl = result.Data.ReturnUrl
        });
    }

    // =========================================================================
    // 4. XÁC THỰC MÃ OTP (VERIFY OTP CHO CẢ READER 2FA & STAFF GMAIL)
    // =========================================================================
    [HttpGet]
    [AllowAnonymous]
    public IActionResult VerifyOtp(string email, string purpose, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(purpose))
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new VerifyOtpViewModel
        {
            Email = email,
            MaskedEmail = StringUtil.MaskEmail(email),
            Purpose = purpose,
            ReturnUrl = returnUrl
        });
    }

    public class VerifyOtpSubmitRequest
    {
        public string Email { get; set; } = string.Empty;
        public string OtpCode { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string? ReturnUrl { get; set; }
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyLoginOtp([FromBody] VerifyOtpSubmitRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.OtpCode))
        {
            return AjaxError("Vui lòng nhập đầy đủ 6 chữ số mã OTP.");
        }

        ServiceResult<ClaimsPrincipal> result;

        if (request.Purpose == AuthService.PurposeReaderLogin)
        {
            result = await authService.CompleteReaderLoginOtpAsync(request.Email, request.OtpCode, ClientIp);
        }
        else if (request.Purpose == AuthService.PurposeStaffLogin)
        {
            result = await authService.CompleteStaffLoginOtpAsync(request.Email, request.OtpCode, ClientIp);
        }
        else
        {
            return AjaxError("Mục đích xác thực không hợp lệ (SEC-12).");
        }

        if (!result.Succeeded || result.Data == null)
        {
            return AjaxError(result.Message, result.Errors);
        }

        // Cấp Cookie Authentication cho trình duyệt (SEC-02, SEC-03)
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            result.Data,
            authProperties);

        var redirectUrl = !string.IsNullOrWhiteSpace(request.ReturnUrl) && Url.IsLocalUrl(request.ReturnUrl)
            ? request.ReturnUrl
            : Url.Action("Index", "Home");

        SetSuccessMessage("Đăng nhập thành công! Chào mừng bạn quay trở lại.");
        return AjaxSuccess(result.Message, new { redirectUrl });
    }

    public class ResendOtpRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Purpose))
        {
            return AjaxError("Thông tin yêu cầu không hợp lệ.");
        }

        var result = await authService.ResendOtpAsync(request.Email, request.Purpose, ClientIp);
        if (!result.Succeeded)
        {
            return AjaxError(result.Message, result.Errors);
        }

        return AjaxSuccess(result.Message, new
        {
            remainingCooldown = EmailService.CooldownSeconds
        });
    }

    // =========================================================================
    // 5. ĐĂNG XUẤT (LOGOUT)
    // =========================================================================
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        SetSuccessMessage("Bạn đã đăng xuất khỏi hệ thống thành công.");
        return RedirectToAction("Index", "Home");
    }

    // =========================================================================
    // 6. TRANG TỪ CHỐI TRUY CẬP (ACCESS DENIED)
    // =========================================================================
    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }
}
