using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Data;
using web_mvc_management.Entity;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Auth;
using web_mvc_management.Util;

namespace web_mvc_management.Services;

public class AuthService(
    ApplicationDbContext context,
    IOtpService otpService,
    IEmailService emailService,
    IIpRateLimiterService rateLimiter,
    IDateTimeProvider dateTimeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public const string PurposeRegister = "REGISTER";
    public const string PurposeReaderLogin = "READER_LOGIN";
    public const string PurposeStaffLogin = "STAFF_LOGIN";

    public async Task<ServiceResult> SendRegisterOtpAsync(ReaderRegisterViewModel model, string clientIp, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        // SEC-17: IP Rate Limiting (5 requests gửi OTP / phút)
        if (!rateLimiter.IsAllowed(clientIp, "SEND_OTP", 5))
        {
            return ServiceResult.Failure("Bạn đã yêu cầu gửi mã quá nhiều lần từ IP này. Vui lòng thử lại sau 1 phút.");
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var normalizedUsername = model.Username.Trim().ToLowerInvariant();
        var normalizedPhone = model.Phone.Trim();

        // SEC-22: Kiểm tra trùng lặp CSDL
        if (await context.Readers.AnyAsync(x => x.Username.ToLower() == normalizedUsername, ct))
        {
            return ServiceResult.ValidationError("Tên đăng nhập này đã được sử dụng.");
        }

        if (await context.Readers.AnyAsync(x => x.Email.ToLower() == normalizedEmail, ct))
        {
            return ServiceResult.ValidationError("Địa chỉ email này đã được đăng ký.");
        }

        if (await context.Readers.AnyAsync(x => x.Phone == normalizedPhone, ct))
        {
            return ServiceResult.ValidationError("Số điện thoại này đã được đăng ký.");
        }

        if (!await context.ReaderTypes.AnyAsync(x => x.Id == model.ReaderTypeId && x.Status == EntityStatus.Active, ct))
        {
            return ServiceResult.ValidationError("Loại độc giả được chọn không hợp lệ.");
        }

        // Kiểm tra Cooldown gửi OTP
        var remainingCooldown = emailService.GetRemainingCooldownSeconds(normalizedEmail);
        if (remainingCooldown > 0)
        {
            return ServiceResult.Failure($"Vui lòng chờ {remainingCooldown} giây trước khi yêu cầu gửi lại mã OTP.");
        }

        // Sinh OTP & Gửi Email
        var otpCode = await otpService.GenerateOtpAsync(normalizedEmail, PurposeRegister, cancellationToken: ct);
        var sendResult = await emailService.SendOtpEmailAsync(normalizedEmail, otpCode, PurposeRegister, ct);

        if (!sendResult.Succeeded)
        {
            return sendResult;
        }

        return ServiceResult.Success("Mã OTP đã được gửi tới email của bạn. Vui lòng kiểm tra hộp thư.");
    }

    public async Task<ServiceResult<Reader>> CompleteRegistrationAsync(ReaderRegisterViewModel model, string otpCode, string clientIp, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        // SEC-17: IP Rate Limiting (15 lần verify / phút)
        if (!rateLimiter.IsAllowed(clientIp, "VERIFY_OTP", 15))
        {
            return ServiceResult<Reader>.Failure("Quá nhiều lượt xác thực từ IP của bạn. Vui lòng thử lại sau 1 phút.");
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();

        // SEC-04 & SEC-12: Verify OTP
        var verifyResult = await otpService.VerifyOtpAsync(normalizedEmail, PurposeRegister, otpCode, ct);
        if (!verifyResult.IsSuccess)
        {
            return ServiceResult<Reader>.Failure(verifyResult.Message);
        }

        // Lấy thông tin loại độc giả để tính hạn thẻ
        var readerType = await context.ReaderTypes.FirstOrDefaultAsync(x => x.Id == model.ReaderTypeId, ct);
        if (readerType == null)
        {
            return ServiceResult<Reader>.Failure("Loại độc giả không tồn tại.");
        }

        var today = dateTimeProvider.Today;
        var validMonths = readerType.CardValidMonths > 0 ? readerType.CardValidMonths : 48;
        var cardExpiry = today.AddMonths(validMonths);

        // Sinh số thẻ thư viện duy nhất
        var count = await context.Readers.CountAsync(ct) + 1;
        var cardNo = $"SV{today.Year}{count:D5}";

        var reader = new Reader
        {
            Id = Guid.NewGuid(),
            ReaderTypeId = model.ReaderTypeId,
            Username = model.Username.Trim(),
            Email = normalizedEmail,
            PasswordHash = PasswordHasher.HashPassword(model.Password),
            FullName = model.FullName.Trim(),
            Phone = model.Phone.Trim(),
            DateOfBirth = model.DateOfBirth,
            Address = model.Address?.Trim(),
            MemberCode = model.MemberCode?.Trim(),
            CardNo = cardNo,
            CardIssuedDate = today,
            CardExpiryDate = cardExpiry,
            CardStatus = LibraryConstants.CardStatus.Active,
            Status = EntityStatus.Active,
            SecurityStamp = Guid.NewGuid().ToString(),
            FailedLoginAttempts = 0,
            CreatedAt = dateTimeProvider.UtcNow,
            UpdatedAt = dateTimeProvider.UtcNow
        };

        await context.Readers.AddAsync(reader, ct);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Độc giả mới đăng ký thành công: {Username} ({CardNo})", reader.Username, reader.CardNo);
        return ServiceResult<Reader>.Success(reader, "Đăng ký thành công! Thẻ thư viện của bạn đã được kích hoạt.");
    }

    public async Task<ServiceResult<LoginStepResult>> InitiateReaderLoginAsync(ReaderLoginViewModel model, string clientIp, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        // SEC-17: IP Rate Limiting (30 requests đăng nhập / phút)
        if (!rateLimiter.IsAllowed(clientIp, "LOGIN", 30))
        {
            return ServiceResult<LoginStepResult>.Failure("Bạn đã thử đăng nhập quá nhiều lần. Vui lòng thử lại sau 1 phút.");
        }

        var usernameOrEmail = model.UsernameOrEmail.Trim().ToLowerInvariant();
        var now = dateTimeProvider.UtcNow;

        var reader = await context.Readers
            .FirstOrDefaultAsync(x => x.Username.ToLower() == usernameOrEmail || x.Email.ToLower() == usernameOrEmail, ct);

        // SEC-15: Timing Attack Defense nếu tài khoản không tồn tại
        if (reader == null)
        {
            PasswordHasher.VerifyDummyPassword(model.Password);
            return ServiceResult<LoginStepResult>.Failure("Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        // SEC-11: Kiểm tra khóa tài khoản do Brute-force
        if (reader.LockedUntil.HasValue && reader.LockedUntil.Value > now)
        {
            var remainingMinutes = (int)Math.Ceiling((reader.LockedUntil.Value - now).TotalMinutes);
            return ServiceResult<LoginStepResult>.Failure(
                $"Tài khoản đang bị tạm khóa để bảo vệ an toàn. Vui lòng thử lại sau {remainingMinutes} phút.");
        }

        // Kiểm tra trạng thái hoạt động của độc giả
        if (reader.Status != EntityStatus.Active || reader.CardStatus == LibraryConstants.CardStatus.Locked)
        {
            return ServiceResult<LoginStepResult>.Failure(
                reader.CardStatus == LibraryConstants.CardStatus.Locked
                    ? $"Thẻ thư viện của bạn đang bị khóa: {reader.LockReason ?? "Liên hệ thủ thư để được hỗ trợ"}."
                    : "Tài khoản của bạn đã bị vô hiệu hóa.");
        }

        // Kiểm tra mật khẩu PBKDF2
        var isPasswordValid = PasswordHasher.VerifyPassword(model.Password, reader.PasswordHash);
        if (!isPasswordValid)
        {
            reader.FailedLoginAttempts++;
            reader.LastLoginAttempt = now;

            if (reader.FailedLoginAttempts >= MaxFailedLoginAttempts)
            {
                reader.LockedUntil = now.Add(LockoutDuration);
                reader.SecurityStamp = Guid.NewGuid().ToString("N"); // NIST SP 800-63B: Thu hồi mọi phiên đang hoạt động khi bị khóa
                await context.SaveChangesAsync(ct);
                logger.LogWarning("Tài khoản độc giả {Username} bị khóa 15 phút do nhập sai mật khẩu 5 lần", reader.Username);
                return ServiceResult<LoginStepResult>.Failure(
                    "Bạn đã nhập sai mật khẩu 5 lần liên tiếp. Tài khoản đã bị tạm khóa trong 15 phút.");
            }

            await context.SaveChangesAsync(ct);
            var remaining = MaxFailedLoginAttempts - reader.FailedLoginAttempts;
            return ServiceResult<LoginStepResult>.Failure(
                $"Tên đăng nhập hoặc mật khẩu không chính xác. Bạn còn {remaining} lần thử.");
        }

        // Mật khẩu đúng -> Sinh OTP 2FA & Gửi về email của Reader
        var otpCode = await otpService.GenerateOtpAsync(reader.Email, PurposeReaderLogin, cancellationToken: ct);
        var sendResult = await emailService.SendOtpEmailAsync(reader.Email, otpCode, PurposeReaderLogin, ct);

        if (!sendResult.Succeeded)
        {
            return ServiceResult<LoginStepResult>.Failure(sendResult.Message);
        }

        return ServiceResult<LoginStepResult>.Success(new LoginStepResult
        {
            RequiresOtp = true,
            Email = reader.Email,
            MaskedEmail = StringUtil.MaskEmail(reader.Email),
            Purpose = PurposeReaderLogin,
            ReturnUrl = model.ReturnUrl
        }, "Mã xác thực đăng nhập 2 bước (2FA) đã được gửi đến email của bạn.");
    }

    public async Task<ServiceResult<ClaimsPrincipal>> CompleteReaderLoginOtpAsync(string email, string otpCode, string clientIp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otpCode))
        {
            return ServiceResult<ClaimsPrincipal>.Failure("Thông tin xác thực không đầy đủ.");
        }

        if (!rateLimiter.IsAllowed(clientIp, "VERIFY_OTP", 15))
        {
            return ServiceResult<ClaimsPrincipal>.Failure("Quá nhiều lượt thử xác thực từ IP của bạn. Vui lòng thử lại sau 1 phút.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var reader = await context.Readers.FirstOrDefaultAsync(x => x.Email == normalizedEmail, ct);
        if (reader == null)
        {
            return ServiceResult<ClaimsPrincipal>.Failure("Không tìm thấy thông tin tài khoản độc giả.");
        }

        var now = dateTimeProvider.UtcNow;
        if (reader.LockedUntil.HasValue && reader.LockedUntil.Value > now)
        {
            var remainingMinutes = (int)Math.Ceiling((reader.LockedUntil.Value - now).TotalMinutes);
            return ServiceResult<ClaimsPrincipal>.Failure(
                $"Tài khoản đang bị tạm khóa. Vui lòng thử lại sau {remainingMinutes} phút.");
        }

        // SEC-04: Xác thực OTP
        var verifyResult = await otpService.VerifyOtpAsync(normalizedEmail, PurposeReaderLogin, otpCode, ct);
        if (!verifyResult.IsSuccess)
        {
            reader.FailedLoginAttempts++;
            reader.LastLoginAttempt = now;
            if (reader.FailedLoginAttempts >= MaxFailedLoginAttempts)
            {
                reader.LockedUntil = now.Add(LockoutDuration);
                reader.SecurityStamp = Guid.NewGuid().ToString("N");
            }
            await context.SaveChangesAsync(ct);
            return ServiceResult<ClaimsPrincipal>.Failure(verifyResult.Message);
        }

        // Đăng nhập thành công -> Reset bộ đếm lỗi & Cập nhật LastLoginAt
        reader.FailedLoginAttempts = 0;
        reader.LockedUntil = null;
        reader.LastLoginAt = now;
        await context.SaveChangesAsync(ct);

        // Tạo ClaimsPrincipal cho Cookie Authentication
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, reader.Id.ToString()),
            new(ClaimTypes.Name, reader.Username),
            new(ClaimTypes.Email, reader.Email),
            new(ClaimTypes.Role, AppRoles.Reader),
            new("FullName", reader.FullName),
            new("CardNo", reader.CardNo),
            new(SecurityStampValidator.SecurityStampClaimType, reader.SecurityStamp)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        logger.LogInformation("Độc giả đăng nhập thành công: {Username} ({CardNo})", reader.Username, reader.CardNo);
        return ServiceResult<ClaimsPrincipal>.Success(principal, "Đăng nhập thành công!");
    }

    public async Task<ServiceResult<LoginStepResult>> InitiateStaffLoginAsync(StaffLoginViewModel model, string clientIp, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!rateLimiter.IsAllowed(clientIp, "LOGIN", 30))
        {
            return ServiceResult<LoginStepResult>.Failure("Bạn đã thử đăng nhập quá nhiều lần. Vui lòng thử lại sau 1 phút.");
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var now = dateTimeProvider.UtcNow;

        var staff = await context.Staff.FirstOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail, ct);

        // SEC-09: Account Enumeration Defense - Không báo email không tồn tại!
        if (staff == null || staff.Status != EntityStatus.Active)
        {
            // SEC-15: Chạy dummy PBKDF2
            PasswordHasher.VerifyDummyPassword("DummyCheckPassword");
            return ServiceResult<LoginStepResult>.Success(new LoginStepResult
            {
                RequiresOtp = true,
                Email = normalizedEmail,
                MaskedEmail = StringUtil.MaskEmail(normalizedEmail),
                Purpose = PurposeStaffLogin,
                ReturnUrl = model.ReturnUrl
            }, "Mã xác thực OTP đã được gửi đến email của bạn nếu thông tin hợp lệ.");
        }

        // SEC-11: Kiểm tra khóa tài khoản
        if (staff.LockedUntil.HasValue && staff.LockedUntil.Value > now)
        {
            var remainingMinutes = (int)Math.Ceiling((staff.LockedUntil.Value - now).TotalMinutes);
            return ServiceResult<LoginStepResult>.Failure(
                $"Tài khoản cán bộ đang bị tạm khóa. Vui lòng thử lại sau {remainingMinutes} phút.");
        }

        // Sinh OTP & Gửi Gmail
        var otpCode = await otpService.GenerateOtpAsync(staff.Email, PurposeStaffLogin, cancellationToken: ct);
        var sendResult = await emailService.SendOtpEmailAsync(staff.Email, otpCode, PurposeStaffLogin, ct);

        if (!sendResult.Succeeded)
        {
            return ServiceResult<LoginStepResult>.Failure(sendResult.Message);
        }

        return ServiceResult<LoginStepResult>.Success(new LoginStepResult
        {
            RequiresOtp = true,
            Email = staff.Email,
            MaskedEmail = StringUtil.MaskEmail(staff.Email),
            Purpose = PurposeStaffLogin,
            ReturnUrl = model.ReturnUrl
        }, "Mã xác thực OTP đã được gửi về Gmail của bạn.");
    }

    public async Task<ServiceResult<ClaimsPrincipal>> CompleteStaffLoginOtpAsync(string email, string otpCode, string clientIp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otpCode))
        {
            return ServiceResult<ClaimsPrincipal>.Failure("Thông tin xác thực không đầy đủ.");
        }

        if (!rateLimiter.IsAllowed(clientIp, "VERIFY_OTP", 15))
        {
            return ServiceResult<ClaimsPrincipal>.Failure("Quá nhiều lượt thử xác thực từ IP của bạn. Vui lòng thử lại sau 1 phút.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var staff = await context.Staff.FirstOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail, ct);
        if (staff == null)
        {
            return ServiceResult<ClaimsPrincipal>.Failure("Không tìm thấy thông tin cán bộ thư viện.");
        }

        var now = dateTimeProvider.UtcNow;
        if (staff.LockedUntil.HasValue && staff.LockedUntil.Value > now)
        {
            var remainingMinutes = (int)Math.Ceiling((staff.LockedUntil.Value - now).TotalMinutes);
            return ServiceResult<ClaimsPrincipal>.Failure(
                $"Tài khoản đang bị tạm khóa. Vui lòng thử lại sau {remainingMinutes} phút.");
        }

        // SEC-04 & SEC-12: Xác thực OTP với Purpose = STAFF_LOGIN
        var verifyResult = await otpService.VerifyOtpAsync(normalizedEmail, PurposeStaffLogin, otpCode, ct);
        if (!verifyResult.IsSuccess)
        {
            staff.FailedLoginAttempts++;
            staff.LastLoginAttempt = now;
            if (staff.FailedLoginAttempts >= MaxFailedLoginAttempts)
            {
                staff.LockedUntil = now.Add(LockoutDuration);
                staff.SecurityStamp = Guid.NewGuid().ToString("N");
            }
            await context.SaveChangesAsync(ct);
            return ServiceResult<ClaimsPrincipal>.Failure(verifyResult.Message);
        }

        // Đăng nhập thành công -> Reset lỗi
        staff.FailedLoginAttempts = 0;
        staff.LockedUntil = null;
        staff.LastLoginAt = now;
        await context.SaveChangesAsync(ct);

        // Gán Role chuẩn: Admin hoặc Librarian từ trường role của Staff
        var assignedRole = staff.Role == AppRoles.Admin ? AppRoles.Admin : AppRoles.Librarian;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
            new(ClaimTypes.Name, staff.Username),
            new(ClaimTypes.Email, staff.Email),
            new(ClaimTypes.Role, assignedRole),
            new("FullName", staff.FullName),
            new(SecurityStampValidator.SecurityStampClaimType, staff.SecurityStamp)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        logger.LogInformation("Cán bộ thư viện đăng nhập thành công: {Username} (Role: {Role})", staff.Username, assignedRole);
        return ServiceResult<ClaimsPrincipal>.Success(principal, "Đăng nhập thành công!");
    }

    public async Task<ServiceResult> ResendOtpAsync(string email, string purpose, string clientIp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(purpose))
        {
            return ServiceResult.Failure("Thông tin yêu cầu không hợp lệ.");
        }

        if (!rateLimiter.IsAllowed(clientIp, "SEND_OTP", 5))
        {
            return ServiceResult.Failure("Bạn đã yêu cầu gửi mã quá nhiều lần từ IP này. Vui lòng thử lại sau 1 phút.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var remainingCooldown = emailService.GetRemainingCooldownSeconds(normalizedEmail);
        if (remainingCooldown > 0)
        {
            return ServiceResult.Failure($"Vui lòng chờ {remainingCooldown} giây trước khi yêu cầu gửi lại mã OTP.");
        }

        var otpCode = await otpService.GenerateOtpAsync(normalizedEmail, purpose, cancellationToken: ct);
        return await emailService.SendOtpEmailAsync(normalizedEmail, otpCode, purpose, ct);
    }
}
