using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Helper;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Auth;
using web_mvc_management.Repositories;
using web_mvc_management.Util;

namespace web_mvc_management.Services;

public class AuthService(
    IUnitOfWork unitOfWork,
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
            return ServiceResult.Failure(ErrorCode.RateLimitOtpExceeded);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var normalizedUsername = model.Username.Trim().ToLowerInvariant();
        var normalizedPhone = model.Phone.Trim();
        var normalizedMemberCode = string.IsNullOrWhiteSpace(model.MemberCode) ? null : model.MemberCode.Trim();

        // SEC-22: Kiểm tra trùng lặp CSDL qua Repository
        if (await unitOfWork.Readers.ExistsAsync(x => x.Username.ToLower() == normalizedUsername, ct))
        {
            return ServiceResult.ValidationError(ErrorCode.RegisterDuplicateUsername);
        }

        if (await unitOfWork.Readers.ExistsAsync(x => x.Email.ToLower() == normalizedEmail, ct))
        {
            return ServiceResult.ValidationError(ErrorCode.RegisterDuplicateEmail);
        }

        if (await unitOfWork.Readers.ExistsAsync(x => x.Phone == normalizedPhone, ct))
        {
            return ServiceResult.ValidationError(ErrorCode.RegisterDuplicatePhone);
        }

        if (normalizedMemberCode != null &&
            await unitOfWork.Readers.ExistsAsync(x => x.MemberCode != null && x.MemberCode.ToLower() == normalizedMemberCode.ToLower(), ct))
        {
            return ServiceResult.ValidationError("Mã sinh viên / Mã cán bộ này đã được đăng ký thẻ thư viện.");
        }

        if (!await unitOfWork.ReaderTypes.ExistsAsync(x => x.Id == model.ReaderTypeId && x.Status == EntityStatus.Active, ct))
        {
            return ServiceResult.ValidationError(ErrorCode.RegisterInvalidReaderType);
        }

        // Kiểm tra Cooldown gửi OTP
        var remainingCooldown = emailService.GetRemainingCooldownSeconds(normalizedEmail);
        if (remainingCooldown > 0)
        {
            return ServiceResult.Failure(ErrorMessages.Format(ErrorCode.OtpCooldownActive, remainingCooldown));
        }

        // Sinh OTP & Gửi Email
        var otpCode = await otpService.GenerateOtpAsync(normalizedEmail, PurposeRegister, cancellationToken: ct);
        var sendResult = await emailService.SendOtpEmailAsync(normalizedEmail, otpCode, PurposeRegister, ct);

        if (!sendResult.Succeeded)
        {
            return sendResult;
        }

        return ServiceResult.Success(sendResult.Message);
    }

    public async Task<ServiceResult<Reader>> CompleteRegistrationAsync(ReaderRegisterViewModel model, string otpCode, string clientIp, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        // SEC-17: IP Rate Limiting (15 lần verify / phút)
        if (!rateLimiter.IsAllowed(clientIp, "VERIFY_OTP", 15))
        {
            return ServiceResult<Reader>.Failure(ErrorCode.RateLimitOtpExceeded);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();

        // SEC-04 & SEC-12: Verify OTP
        var verifyResult = await otpService.VerifyOtpAsync(normalizedEmail, PurposeRegister, otpCode, ct);
        if (!verifyResult.IsSuccess)
        {
            return ServiceResult<Reader>.Failure(verifyResult.Message);
        }

        var normalizedUsername = model.Username.Trim();
        var normalizedPhone = model.Phone.Trim();
        var normalizedMemberCode = string.IsNullOrWhiteSpace(model.MemberCode) ? null : model.MemberCode.Trim();

        await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct);
        try
        {
            if (await unitOfWork.Readers.ExistsAsync(x => x.Username.ToLower() == normalizedUsername.ToLower(), ct))
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult<Reader>.ValidationError(ErrorCode.RegisterDuplicateUsername);
            }

            if (await unitOfWork.Readers.ExistsAsync(x => x.Email.ToLower() == normalizedEmail, ct))
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult<Reader>.ValidationError(ErrorCode.RegisterDuplicateEmail);
            }

            if (await unitOfWork.Readers.ExistsAsync(x => x.Phone == normalizedPhone, ct))
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult<Reader>.ValidationError(ErrorCode.RegisterDuplicatePhone);
            }

            if (normalizedMemberCode != null &&
                await unitOfWork.Readers.ExistsAsync(x => x.MemberCode != null && x.MemberCode.ToLower() == normalizedMemberCode.ToLower(), ct))
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult<Reader>.ValidationError("Mã sinh viên / Mã cán bộ này đã được đăng ký thẻ thư viện.");
            }

            // Lấy thông tin loại độc giả để tính hạn thẻ
            var readerType = await unitOfWork.ReaderTypes.FirstOrDefaultAsync(
                x => x.Id == model.ReaderTypeId && x.Status == EntityStatus.Active,
                asNoTracking: true,
                cancellationToken: ct);

            if (readerType == null)
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return ServiceResult<Reader>.Failure(ErrorCode.RegisterInvalidReaderType);
            }

            var today = dateTimeProvider.Today;
            var validMonths = readerType.CardValidMonths > 0 ? readerType.CardValidMonths : 48;
            var cardExpiry = today.AddMonths(validMonths);

            // Sinh số thẻ thư viện duy nhất
            var cardPrefix = readerType.Code.ToUpperInvariant() switch
            {
                "LECTURER" => "GV",
                "GUEST" => "DG",
                _ => "SV"
            };
            var sequence = await unitOfWork.Readers.CountAsync(cancellationToken: ct) + 1;
            string cardNo;
            do
            {
                cardNo = $"{cardPrefix}{today.Year}{sequence:D5}";
                sequence++;
            } while (await unitOfWork.Readers.ExistsAsync(x => x.CardNo == cardNo, ct));

            var reader = new Reader
            {
                Id = Guid.NewGuid(),
                ReaderTypeId = model.ReaderTypeId,
                Username = normalizedUsername,
                Email = normalizedEmail,
                PasswordHash = PasswordHasher.HashPassword(model.Password),
                FullName = model.FullName.Trim(),
                Phone = normalizedPhone,
                DateOfBirth = model.DateOfBirth,
                Address = model.Address?.Trim(),
                MemberCode = normalizedMemberCode,
                CardNo = cardNo,
                CardIssuedDate = today,
                CardExpiryDate = cardExpiry,
                CardStatus = LibraryConstants.CardStatus.Active,
                Status = EntityStatus.Active,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                FailedLoginAttempts = 0,
                CreatedAt = dateTimeProvider.UtcNow,
                UpdatedAt = dateTimeProvider.UtcNow
            };

            await unitOfWork.Readers.AddAsync(reader, ct);
            await unitOfWork.CommitTransactionAsync(ct);

            logger.LogInformation("Độc giả mới đăng ký thành công: {Username} ({CardNo})", reader.Username, reader.CardNo);
            return ServiceResult<Reader>.Success(reader, "Đã mở thẻ thư viện.");
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult<LoginStepResult>> InitiateReaderLoginAsync(ReaderLoginViewModel model, string clientIp, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        // SEC-17: IP Rate Limiting (30 requests đăng nhập / phút)
        if (!rateLimiter.IsAllowed(clientIp, "LOGIN", 30))
        {
            return ServiceResult<LoginStepResult>.Failure(ErrorCode.RateLimitLoginExceeded);
        }

        var usernameOrEmail = model.UsernameOrEmail.Trim().ToLowerInvariant();
        var now = dateTimeProvider.UtcNow;

        var reader = await unitOfWork.Readers.FirstOrDefaultAsync(
            x => x.Username.ToLower() == usernameOrEmail || x.Email.ToLower() == usernameOrEmail,
            asNoTracking: false,
            cancellationToken: ct);

        // SEC-15: Timing Attack Defense nếu tài khoản không tồn tại
        if (reader == null)
        {
            PasswordHasher.VerifyDummyPassword(model.Password);
            return ServiceResult<LoginStepResult>.Failure(ErrorCode.AuthInvalidCredentials);
        }

        // SEC-11: Kiểm tra khóa tài khoản do Brute-force
        if (reader.LockedUntil.HasValue)
        {
            if (reader.LockedUntil.Value > now)
            {
                var remainingMinutes = (int)Math.Ceiling((reader.LockedUntil.Value - now).TotalMinutes);
                return ServiceResult<LoginStepResult>.Failure(
                    ErrorMessages.Format(ErrorCode.AuthAccountLocked, remainingMinutes, DateTimeHelper.ToVietnamTimeString(reader.LockedUntil.Value)));
            }

            // Nếu thời gian khóa đã hết hạn, reset bộ đếm để cấp lại chu kỳ thử mới
            reader.LockedUntil = null;
            reader.FailedLoginAttempts = 0;
        }

        // Kiểm tra trạng thái hoạt động của độc giả
        if (reader.Status != EntityStatus.Active || reader.CardStatus == LibraryConstants.CardStatus.Locked)
        {
            return ServiceResult<LoginStepResult>.Failure(
                reader.CardStatus == LibraryConstants.CardStatus.Locked
                    ? ErrorMessages.Format(ErrorCode.AuthCardLocked, reader.LockReason ?? "Liên hệ thủ thư để được hỗ trợ")
                    : ErrorMessages.Get(ErrorCode.AuthAccountInactive));
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
                await unitOfWork.SaveChangesAsync(ct);
                logger.LogWarning("Tài khoản độc giả {Username} bị khóa 15 phút do nhập sai mật khẩu 5 lần", reader.Username);
                return ServiceResult<LoginStepResult>.Failure(
                    ErrorMessages.Format(ErrorCode.AuthMaxAttemptsExceeded, DateTimeHelper.ToVietnamTimeString(reader.LockedUntil.Value)));
            }

            await unitOfWork.SaveChangesAsync(ct);
            var remaining = MaxFailedLoginAttempts - reader.FailedLoginAttempts;
            return ServiceResult<LoginStepResult>.Failure(
                ErrorMessages.Format(ErrorCode.AuthAttemptsRemaining, remaining));
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
        }, sendResult.Message);
    }

    public async Task<ServiceResult<ClaimsPrincipal>> CompleteReaderLoginOtpAsync(string email, string otpCode, string clientIp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otpCode))
        {
            return ServiceResult<ClaimsPrincipal>.Failure(ErrorCode.OtpInfoIncomplete);
        }

        if (!rateLimiter.IsAllowed(clientIp, "VERIFY_OTP", 15))
        {
            return ServiceResult<ClaimsPrincipal>.Failure(ErrorCode.RateLimitOtpExceeded);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var reader = await unitOfWork.Readers.FirstOrDefaultAsync(
            x => x.Email.ToLower() == normalizedEmail,
            asNoTracking: false,
            cancellationToken: ct);

        if (reader == null)
        {
            return ServiceResult<ClaimsPrincipal>.Failure(ErrorCode.AuthInvalidCredentials);
        }

        var now = dateTimeProvider.UtcNow;
        if (reader.LockedUntil.HasValue)
        {
            if (reader.LockedUntil.Value > now)
            {
                var remainingMinutes = (int)Math.Ceiling((reader.LockedUntil.Value - now).TotalMinutes);
                return ServiceResult<ClaimsPrincipal>.Failure(
                    ErrorMessages.Format(ErrorCode.AuthAccountLocked, remainingMinutes, DateTimeHelper.ToVietnamTimeString(reader.LockedUntil.Value)));
            }

            reader.LockedUntil = null;
            reader.FailedLoginAttempts = 0;
        }

        if (reader.Status != EntityStatus.Active || reader.CardStatus == LibraryConstants.CardStatus.Locked)
        {
            return ServiceResult<ClaimsPrincipal>.Failure(
                reader.CardStatus == LibraryConstants.CardStatus.Locked
                    ? ErrorMessages.Format(ErrorCode.AuthCardLocked, reader.LockReason ?? "Liên hệ thủ thư để được hỗ trợ")
                    : ErrorMessages.Get(ErrorCode.AuthAccountInactive));
        }

        // Account state can change after the password step while the OTP is pending.
        if (reader.Status != EntityStatus.Active || reader.CardStatus == LibraryConstants.CardStatus.Locked)
        {
            return ServiceResult<ClaimsPrincipal>.Failure(ErrorCode.AuthAccountInactive);
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
            await unitOfWork.SaveChangesAsync(ct);
            return ServiceResult<ClaimsPrincipal>.Failure(verifyResult.Message);
        }

        // Đăng nhập thành công -> Reset bộ đếm lỗi & Cập nhật LastLoginAt
        reader.FailedLoginAttempts = 0;
        reader.LockedUntil = null;
        reader.LastLoginAt = now;
        await unitOfWork.SaveChangesAsync(ct);

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
            return ServiceResult<LoginStepResult>.Failure(ErrorCode.RateLimitLoginExceeded);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var now = dateTimeProvider.UtcNow;

        var staff = await unitOfWork.Staff.FirstOrDefaultAsync(
            x => x.Email.ToLower() == normalizedEmail,
            asNoTracking: false,
            cancellationToken: ct);

        // SEC-09: Account Enumeration Defense - Không báo email không tồn tại!
        if (staff == null || staff.Status != EntityStatus.Active)
        {
            // SEC-15: Chạy dummy PBKDF2
            PasswordHasher.VerifyDummyPassword(normalizedEmail);
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
        if (staff.LockedUntil.HasValue)
        {
            if (staff.LockedUntil.Value > now)
            {
                var remainingMinutes = (int)Math.Ceiling((staff.LockedUntil.Value - now).TotalMinutes);
                return ServiceResult<LoginStepResult>.Failure(
                    ErrorMessages.Format(ErrorCode.AuthAccountLocked, remainingMinutes, DateTimeHelper.ToVietnamTimeString(staff.LockedUntil.Value)));
            }

            staff.LockedUntil = null;
            staff.FailedLoginAttempts = 0;
            await unitOfWork.SaveChangesAsync(ct);
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
        }, sendResult.Message);
    }

    public async Task<ServiceResult<ClaimsPrincipal>> CompleteStaffLoginOtpAsync(string email, string otpCode, string clientIp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otpCode))
        {
            return ServiceResult<ClaimsPrincipal>.Failure(ErrorCode.OtpInfoIncomplete);
        }

        if (!rateLimiter.IsAllowed(clientIp, "VERIFY_OTP", 15))
        {
            return ServiceResult<ClaimsPrincipal>.Failure(ErrorCode.RateLimitOtpExceeded);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var staff = await unitOfWork.Staff.FirstOrDefaultAsync(
            x => x.Email.ToLower() == normalizedEmail,
            asNoTracking: false,
            cancellationToken: ct);

        if (staff == null || staff.Status != EntityStatus.Active)
        {
            return ServiceResult<ClaimsPrincipal>.Failure(ErrorCode.AuthStaffNotFound);
        }

        var now = dateTimeProvider.UtcNow;
        if (staff.LockedUntil.HasValue)
        {
            if (staff.LockedUntil.Value > now)
            {
                var remainingMinutes = (int)Math.Ceiling((staff.LockedUntil.Value - now).TotalMinutes);
                return ServiceResult<ClaimsPrincipal>.Failure(
                    ErrorMessages.Format(ErrorCode.AuthAccountLocked, remainingMinutes, DateTimeHelper.ToVietnamTimeString(staff.LockedUntil.Value)));
            }

            staff.LockedUntil = null;
            staff.FailedLoginAttempts = 0;
        }

        if (staff.Status != EntityStatus.Active)
        {
            return ServiceResult<ClaimsPrincipal>.Failure(ErrorCode.AuthAccountInactive);
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
            await unitOfWork.SaveChangesAsync(ct);
            return ServiceResult<ClaimsPrincipal>.Failure(verifyResult.Message);
        }

        // Đăng nhập thành công -> Reset lỗi
        staff.FailedLoginAttempts = 0;
        staff.LockedUntil = null;
        staff.LastLoginAt = now;
        await unitOfWork.SaveChangesAsync(ct);

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
            return ServiceResult.Failure(ErrorCode.CommonInvalidRequest);
        }

        if (!rateLimiter.IsAllowed(clientIp, "SEND_OTP", 5))
        {
            return ServiceResult.Failure(ErrorCode.RateLimitOtpExceeded);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var remainingCooldown = emailService.GetRemainingCooldownSeconds(normalizedEmail);
        if (remainingCooldown > 0)
        {
            return ServiceResult.Failure(ErrorMessages.Format(ErrorCode.OtpCooldownActive, remainingCooldown));
        }

        var otpCode = await otpService.GenerateOtpAsync(normalizedEmail, purpose, cancellationToken: ct);
        return await emailService.SendOtpEmailAsync(normalizedEmail, otpCode, purpose, ct);
    }

    public async Task<IReadOnlyList<ReaderType>> GetActiveReaderTypesAsync(CancellationToken ct = default)
    {
        return await unitOfWork.ReaderTypes
            .GetQueryable(asNoTracking: true)
            .Where(x => x.Status == EntityStatus.Active)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }
}
