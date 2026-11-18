using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Constant;
using web_mvc_management.Extensions;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Auth;
using web_mvc_management.Services;
using web_mvc_management.Util;

namespace web_mvc_management.Controllers;

/// <summary>
/// Controller điều hướng xác thực và phân quyền chuẩn Stateful ASP.NET Core MVC (.NET 10.0).
/// </summary>
public class AccountController(
    IAuthService authService,
    IIpRateLimiterService rateLimiter,
    IDateTimeProvider dateTimeProvider) : BaseController
{
    private const string SessionPendingRegistration = "PendingRegistration";
    private const string SessionPendingLoginEmail = "PendingLoginEmail";
    private const string SessionPendingOtpPurpose = "PendingOtpPurpose";
    private const string SessionPendingReturnUrl = "PendingReturnUrl";
    private const string SessionPendingRememberMe = "PendingRememberMe";

    private string ClientIp => rateLimiter.GetClientIp(HttpContext);

    // =========================================================================
    // 1. ĐĂNG KÝ MỞ THẺ BẠN ĐỌC (READER REGISTER)
    // =========================================================================
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        await LoadReaderTypesAsync();
        return View(new ReaderRegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(ReaderRegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LoadReaderTypesAsync();
            return View(model);
        }

        var result = await authService.SendRegisterOtpAsync(model, ClientIp);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            await LoadReaderTypesAsync();
            return View(model);
        }

        HttpContext.Session.SetObject(SessionPendingRegistration, model);
        HttpContext.Session.SetString(SessionPendingOtpPurpose, AuthService.PurposeRegister);

        if (result.Message.Contains("Log của Server", StringComparison.OrdinalIgnoreCase))
        {
            SetWarningMessage(result.Message);
        }

        return RedirectToAction(nameof(VerifyOtp), new { purpose = AuthService.PurposeRegister });
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
            return View(model);
        }

        var result = await authService.InitiateReaderLoginAsync(model, ClientIp);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        HttpContext.Session.SetString(SessionPendingLoginEmail, result.Data!.Email);
        HttpContext.Session.SetString(SessionPendingOtpPurpose, AuthService.PurposeReaderLogin);
        HttpContext.Session.SetString(SessionPendingReturnUrl, model.ReturnUrl ?? string.Empty);
        HttpContext.Session.SetString(SessionPendingRememberMe, model.RememberMe.ToString());

        if (result.Message.Contains("Log của Server", StringComparison.OrdinalIgnoreCase))
        {
            SetWarningMessage(result.Message);
        }

        return RedirectToAction(nameof(VerifyOtp), new { purpose = AuthService.PurposeReaderLogin });
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
            return View(model);
        }

        var result = await authService.InitiateStaffLoginAsync(model, ClientIp);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        HttpContext.Session.SetString(SessionPendingLoginEmail, model.Email.Trim().ToLowerInvariant());
        HttpContext.Session.SetString(SessionPendingOtpPurpose, AuthService.PurposeStaffLogin);
        HttpContext.Session.SetString(SessionPendingReturnUrl, model.ReturnUrl ?? string.Empty);

        if (result.Message.Contains("Log của Server", StringComparison.OrdinalIgnoreCase))
        {
            SetWarningMessage(result.Message);
        }

        return RedirectToAction(nameof(VerifyOtp), new { purpose = AuthService.PurposeStaffLogin });
    }

    // =========================================================================
    // 4. XÁC THỰC MÃ OTP (VERIFY OTP)
    // =========================================================================
    [HttpGet]
    [AllowAnonymous]
    public IActionResult VerifyOtp(string? purpose = null)
    {
        var sessionPurpose = HttpContext.Session.GetString(SessionPendingOtpPurpose);
        var effectivePurpose = !string.IsNullOrWhiteSpace(sessionPurpose)
            ? sessionPurpose
            : (purpose ?? AuthService.PurposeReaderLogin);

        if (!string.IsNullOrWhiteSpace(sessionPurpose) &&
            !string.IsNullOrWhiteSpace(purpose) &&
            !string.Equals(sessionPurpose, purpose, StringComparison.Ordinal))
        {
            effectivePurpose = sessionPurpose;
        }

        string targetEmail;
        if (effectivePurpose == AuthService.PurposeRegister)
        {
            var pendingReg = HttpContext.Session.GetObject<ReaderRegisterViewModel>(SessionPendingRegistration);
            if (pendingReg == null)
            {
                SetErrorMessage(ErrorMessages.Get(ErrorCode.RegisterSessionExpired));
                return RedirectToAction(nameof(Register));
            }
            targetEmail = pendingReg.Email;
        }
        else
        {
            var email = HttpContext.Session.GetString(SessionPendingLoginEmail);
            if (string.IsNullOrWhiteSpace(email))
            {
                SetErrorMessage(ErrorMessages.Get(ErrorCode.AuthSessionExpired));
                return RedirectToAction(effectivePurpose == AuthService.PurposeStaffLogin ? nameof(StaffLogin) : nameof(Login));
            }
            targetEmail = email;
        }

        return View(new VerifyOtpViewModel
        {
            Email = targetEmail,
            MaskedEmail = StringUtil.MaskEmail(targetEmail),
            Purpose = effectivePurpose,
            ReturnUrl = HttpContext.Session.GetString(SessionPendingReturnUrl)
        });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
    {
        var sessionPurpose = HttpContext.Session.GetString(SessionPendingOtpPurpose);
        if (string.IsNullOrWhiteSpace(sessionPurpose) ||
            !string.Equals(model.Purpose, sessionPurpose, StringComparison.Ordinal))
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.OtpPurposeMismatch));
            return RedirectToAction(nameof(Login));
        }

        var pendingReg = sessionPurpose == AuthService.PurposeRegister
            ? HttpContext.Session.GetObject<ReaderRegisterViewModel>(SessionPendingRegistration)
            : null;
        var sessionEmail = sessionPurpose == AuthService.PurposeRegister
            ? pendingReg?.Email
            : HttpContext.Session.GetString(SessionPendingLoginEmail);

        if (string.IsNullOrWhiteSpace(sessionEmail))
        {
            SetErrorMessage(ErrorMessages.Get(
                sessionPurpose == AuthService.PurposeRegister
                    ? ErrorCode.RegisterSessionExpired
                    : ErrorCode.AuthSessionExpired));
            return RedirectToAction(sessionPurpose switch
            {
                AuthService.PurposeRegister => nameof(Register),
                AuthService.PurposeStaffLogin => nameof(StaffLogin),
                _ => nameof(Login)
            });
        }

        model.Email = sessionEmail;
        model.MaskedEmail = StringUtil.MaskEmail(sessionEmail);
        model.Purpose = sessionPurpose;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (sessionPurpose == AuthService.PurposeRegister)
        {
            var result = await authService.CompleteRegistrationAsync(pendingReg!, model.OtpCode, ClientIp);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(nameof(model.OtpCode), result.Message);
                return View(model);
            }

            HttpContext.Session.Remove(SessionPendingRegistration);
            HttpContext.Session.Remove(SessionPendingOtpPurpose);

            SetSuccessMessage($"Đã mở thẻ thư viện số {result.Data?.CardNo}. Đăng nhập để bắt đầu mượn sách.");
            return RedirectToAction(nameof(Login));
        }

        if (sessionPurpose == AuthService.PurposeReaderLogin)
        {
            var result = await authService.CompleteReaderLoginOtpAsync(sessionEmail, model.OtpCode, ClientIp);
            if (!result.Succeeded || result.Data == null)
            {
                ModelState.AddModelError(nameof(model.OtpCode), result.Message);
                return View(model);
            }

            var isPersistent = bool.TryParse(HttpContext.Session.GetString(SessionPendingRememberMe), out var rem) && rem;
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                result.Data,
                new AuthenticationProperties
                {
                    IsPersistent = isPersistent,
                    ExpiresUtc = dateTimeProvider.UtcNow.AddHours(8)
                });

            var returnUrl = HttpContext.Session.GetString(SessionPendingReturnUrl);
            ClearPendingLoginSession();

            SetSuccessMessage("Đã đăng nhập.");
            return RedirectToLocal(returnUrl);
        }

        if (sessionPurpose == AuthService.PurposeStaffLogin)
        {
            var result = await authService.CompleteStaffLoginOtpAsync(sessionEmail, model.OtpCode, ClientIp);
            if (!result.Succeeded || result.Data == null)
            {
                ModelState.AddModelError(nameof(model.OtpCode), result.Message);
                return View(model);
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                result.Data,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = dateTimeProvider.UtcNow.AddHours(8)
                });

            var returnUrl = HttpContext.Session.GetString(SessionPendingReturnUrl);
            ClearPendingLoginSession();

            SetSuccessMessage("Đã đăng nhập.");
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return Redirect(Screens.GetPath(ScreenId.ManageBooks));
        }

        ModelState.AddModelError(string.Empty, ErrorMessages.Get(ErrorCode.OtpPurposeMismatch));
        return View(model);
    }

    // =========================================================================
    // 5. GỬI LẠI MÃ OTP (RESEND OTP)
    // =========================================================================
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp(string purpose)
    {
        var sessionPurpose = HttpContext.Session.GetString(SessionPendingOtpPurpose);
        if (string.IsNullOrWhiteSpace(sessionPurpose) ||
            !string.Equals(purpose, sessionPurpose, StringComparison.Ordinal))
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.OtpPurposeMismatch));
            return RedirectToAction(nameof(Login));
        }

        var email = sessionPurpose == AuthService.PurposeRegister
            ? HttpContext.Session.GetObject<ReaderRegisterViewModel>(SessionPendingRegistration)?.Email
            : HttpContext.Session.GetString(SessionPendingLoginEmail);

        if (string.IsNullOrWhiteSpace(email))
        {
            SetErrorMessage(ErrorMessages.Get(ErrorCode.OtpNotFound));
            return RedirectToAction(sessionPurpose == AuthService.PurposeStaffLogin ? nameof(StaffLogin) : nameof(Login));
        }

        return RedirectWithFlash(
            await authService.ResendOtpAsync(email, sessionPurpose, ClientIp),
            nameof(VerifyOtp),
            routeValues: new { purpose = sessionPurpose });
    }

    // =========================================================================
    // 6. ĐĂNG XUẤT (LOGOUT) & ACCESS DENIED
    // =========================================================================
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        SetSuccessMessage("Đã đăng xuất.");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private void ClearPendingLoginSession()
    {
        HttpContext.Session.Remove(SessionPendingLoginEmail);
        HttpContext.Session.Remove(SessionPendingOtpPurpose);
        HttpContext.Session.Remove(SessionPendingReturnUrl);
        HttpContext.Session.Remove(SessionPendingRememberMe);
    }

    private async Task LoadReaderTypesAsync() => ViewBag.ReaderTypes = await authService.GetActiveReaderTypesAsync();

    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Home");
}
