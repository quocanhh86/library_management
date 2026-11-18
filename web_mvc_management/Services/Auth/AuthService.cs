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

    public const string PurposeRegister = LibraryConstants.OtpPurpose.Register;
    public const string PurposeReaderLogin = LibraryConstants.OtpPurpose.ReaderLogin;
    public const string PurposeStaffLogin = LibraryConstants.OtpPurpose.StaffLogin;

    public async Task<ServiceResult> SendRegisterOtpAsync(ReaderRegisterViewModel model, string clientIp, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!rateLimiter.IsAllowed(clientIp, "SEND_OTP", 5))
        {
            return ServiceResult.Failure(ErrorCode.RateLimitOtpExceeded);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var duplicateError = await CheckReaderDuplicatesAsync(
            model.Username.Trim(),
            normalizedEmail,
            model.Phone.Trim(),
            NormalizeOptional(model.MemberCode),
            ct);
        if (duplicateError != null)
        {
            return duplicateError;
        }

        if (!await unitOfWork.ReaderTypes.ExistsAsync(x => x.Id == model.ReaderTypeId && x.Status == EntityStatus.Active, ct))
        {
            return ServiceResult.ValidationError(ErrorCode.RegisterInvalidReaderType);
        }

        var remainingCooldown = emailService.GetRemainingCooldownSeconds(normalizedEmail);
        if (remainingCooldown > 0)
        {
            return ServiceResult.Failure(ErrorMessages.Format(ErrorCode.OtpCooldownActive, remainingCooldown));
        }

        var otpCode = await otpService.GenerateOtpAsync(normalizedEmail, PurposeRegister, cancellationToken: ct);
        var sendResult = await emailService.SendOtpEmailAsync(normalizedEmail, otpCode, PurposeRegister, ct);

        return sendResult.Succeeded
            ? ServiceResult.Success(sendResult.Message)
            : sendResult;
    }

    public async Task<ServiceResult<RegisteredReaderViewModel>> CompleteRegistrationAsync(
        ReaderRegisterViewModel model,
        string otpCode,
        string clientIp,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!rateLimiter.IsAllowed(clientIp, "VERIFY_OTP", 15))
        {
            return ServiceResult<RegisteredReaderViewModel>.Failure(ErrorCode.RateLimitOtpExceeded);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var verifyResult = await otpService.VerifyOtpAsync(normalizedEmail, PurposeRegister, otpCode, ct);
        if (!verifyResult.IsSuccess)
        {
            return ServiceResult<RegisteredReaderViewModel>.Failure(verifyResult.Message);
        }

        var normalizedUsername = model.Username.Trim();
        var normalizedPhone = model.Phone.Trim();
        var normalizedMemberCode = NormalizeOptional(model.MemberCode);

        return await unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var duplicateError = await CheckReaderDuplicatesAsync(
                normalizedUsername,
                normalizedEmail,
                normalizedPhone,
                normalizedMemberCode,
                ct);
            if (duplicateError != null)
            {
                return ServiceResult<RegisteredReaderViewModel>.ValidationError(
                    duplicateError.Code ?? ErrorCode.CommonValidationError,
                    duplicateError.Message);
            }

            var readerType = await unitOfWork.ReaderTypes.FirstOrDefaultAsync(
                x => x.Id == model.ReaderTypeId && x.Status == EntityStatus.Active,
                asNoTracking: true,
                cancellationToken: ct);
            if (readerType == null)
            {
                return ServiceResult<RegisteredReaderViewModel>.Failure(ErrorCode.RegisterInvalidReaderType);
            }

            var today = dateTimeProvider.Today;
            var validMonths = readerType.CardValidMonths > 0 ? readerType.CardValidMonths : 48;
            var cardNo = await GenerateUniqueCardNumberAsync(readerType.Code, today.Year, ct);
            var now = dateTimeProvider.UtcNow;

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
                CardExpiryDate = today.AddMonths(validMonths),
                CardStatus = LibraryConstants.CardStatus.Active,
                Status = EntityStatus.Active,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                FailedLoginAttempts = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            await unitOfWork.Readers.AddAsync(reader, ct);
            logger.LogInformation("Độc giả mới đăng ký thành công: {Username} ({CardNo})", reader.Username, reader.CardNo);

            return ServiceResult<RegisteredReaderViewModel>.Success(
                new RegisteredReaderViewModel
                {
                    Id = reader.Id,
                    Username = reader.Username,
                    Email = reader.Email,
                    FullName = reader.FullName,
                    CardNo = reader.CardNo
                },
                "Đã mở thẻ thư viện.");
        }, cancellationToken: ct);
    }

    public async Task<ServiceResult<LoginStepResult>> InitiateReaderLoginAsync(
        ReaderLoginViewModel model,
        string clientIp,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

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

        if (reader == null)
        {
            PasswordHasher.VerifyDummyPassword(model.Password);
            return ServiceResult<LoginStepResult>.Failure(ErrorCode.AuthInvalidCredentials);
        }

        if (reader.LockedUntil.HasValue)
        {
            if (reader.LockedUntil.Value > now)
            {
                return ServiceResult<LoginStepResult>.Failure(FormatAccountLockedMessage(reader.LockedUntil.Value, now));
            }

            reader.LockedUntil = null;
            reader.FailedLoginAttempts = 0;
        }

        if (reader.Status != EntityStatus.Active || reader.CardStatus == LibraryConstants.CardStatus.Locked)
        {
            return ServiceResult<LoginStepResult>.Failure(FormatReaderBlockedMessage(reader));
        }

        if (!PasswordHasher.VerifyPassword(model.Password, reader.PasswordHash))
        {
            reader.FailedLoginAttempts++;
            reader.LastLoginAttempt = now;

            if (reader.FailedLoginAttempts >= MaxFailedLoginAttempts)
            {
                reader.LockedUntil = now.Add(LockoutDuration);
                reader.SecurityStamp = Guid.NewGuid().ToString("N");
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

    public async Task<ServiceResult<ClaimsPrincipal>> CompleteReaderLoginOtpAsync(
        string email,
        string otpCode,
        string clientIp,
        CancellationToken ct = default)
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
                return ServiceResult<ClaimsPrincipal>.Failure(FormatAccountLockedMessage(reader.LockedUntil.Value, now));
            }

            reader.LockedUntil = null;
            reader.FailedLoginAttempts = 0;
        }

        if (reader.Status != EntityStatus.Active || reader.CardStatus == LibraryConstants.CardStatus.Locked)
        {
            return ServiceResult<ClaimsPrincipal>.Failure(FormatReaderBlockedMessage(reader));
        }

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

        reader.FailedLoginAttempts = 0;
        reader.LockedUntil = null;
        reader.LastLoginAt = now;
        await unitOfWork.SaveChangesAsync(ct);

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, reader.Id.ToString()),
            new(ClaimTypes.Name, reader.Username),
            new(ClaimTypes.Email, reader.Email),
            new(ClaimTypes.Role, AppRoles.Reader),
            new("FullName", reader.FullName),
            new("CardNo", reader.CardNo),
            new(SecurityStampValidator.SecurityStampClaimType, reader.SecurityStamp)
        ];

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        logger.LogInformation("Độc giả đăng nhập thành công: {Username} ({CardNo})", reader.Username, reader.CardNo);
        return ServiceResult<ClaimsPrincipal>.Success(principal, "Đăng nhập thành công!");
    }

    public async Task<ServiceResult<LoginStepResult>> InitiateStaffLoginAsync(
        StaffLoginViewModel model,
        string clientIp,
        CancellationToken ct = default)
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

        // SEC-09: Account Enumeration Defense - Không báo email không tồn tại
        if (staff == null || staff.Status != EntityStatus.Active)
        {
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

        if (staff.LockedUntil.HasValue)
        {
            if (staff.LockedUntil.Value > now)
            {
                return ServiceResult<LoginStepResult>.Failure(FormatAccountLockedMessage(staff.LockedUntil.Value, now));
            }

            staff.LockedUntil = null;
            staff.FailedLoginAttempts = 0;
            await unitOfWork.SaveChangesAsync(ct);
        }

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

    public async Task<ServiceResult<ClaimsPrincipal>> CompleteStaffLoginOtpAsync(
        string email,
        string otpCode,
        string clientIp,
        CancellationToken ct = default)
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
                return ServiceResult<ClaimsPrincipal>.Failure(FormatAccountLockedMessage(staff.LockedUntil.Value, now));
            }

            staff.LockedUntil = null;
            staff.FailedLoginAttempts = 0;
        }

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

        staff.FailedLoginAttempts = 0;
        staff.LockedUntil = null;
        staff.LastLoginAt = now;
        await unitOfWork.SaveChangesAsync(ct);

        var assignedRole = staff.Role == AppRoles.Admin ? AppRoles.Admin : AppRoles.Librarian;
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
            new(ClaimTypes.Name, staff.Username),
            new(ClaimTypes.Email, staff.Email),
            new(ClaimTypes.Role, assignedRole),
            new("FullName", staff.FullName),
            new(SecurityStampValidator.SecurityStampClaimType, staff.SecurityStamp)
        ];

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        logger.LogInformation("Cán bộ thư viện đăng nhập thành công: {Username} (Role: {Role})", staff.Username, assignedRole);
        return ServiceResult<ClaimsPrincipal>.Success(principal, "Đăng nhập thành công!");
    }

    public async Task<ServiceResult> ResendOtpAsync(
        string email,
        string purpose,
        string clientIp,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(purpose))
        {
            return ServiceResult.Failure(ErrorCode.CommonInvalidRequest);
        }

        if (purpose is not (PurposeRegister or PurposeReaderLogin or PurposeStaffLogin))
        {
            return ServiceResult.Failure(ErrorCode.OtpPurposeMismatch);
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

    public async Task<IReadOnlyList<ReaderTypeOptionViewModel>> GetActiveReaderTypesAsync(CancellationToken ct = default) =>
        await unitOfWork.ReaderTypes
            .GetQueryable(asNoTracking: true)
            .Where(x => x.Status == EntityStatus.Active)
            .OrderBy(x => x.Name)
            .Select(x => new ReaderTypeOptionViewModel
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                MaxBorrowBooks = x.MaxBorrowBooks,
                LoanDays = x.LoanDays
            })
            .ToListAsync(ct);

    // ===== Helpers nội bộ =====

    private async Task<ServiceResult?> CheckReaderDuplicatesAsync(
        string username,
        string normalizedEmail,
        string phone,
        string? memberCode,
        CancellationToken ct)
    {
        if (await unitOfWork.Readers.ExistsAsync(x => x.Username.ToLower() == username.ToLower(), ct))
        {
            return ServiceResult.ValidationError(ErrorCode.RegisterDuplicateUsername);
        }

        if (await unitOfWork.Readers.ExistsAsync(x => x.Email.ToLower() == normalizedEmail, ct))
        {
            return ServiceResult.ValidationError(ErrorCode.RegisterDuplicateEmail);
        }

        if (await unitOfWork.Readers.ExistsAsync(x => x.Phone == phone, ct))
        {
            return ServiceResult.ValidationError(ErrorCode.RegisterDuplicatePhone);
        }

        if (memberCode != null &&
            await unitOfWork.Readers.ExistsAsync(x => x.MemberCode != null && x.MemberCode.ToLower() == memberCode.ToLower(), ct))
        {
            return ServiceResult.ValidationError("Mã sinh viên / Mã cán bộ này đã được đăng ký thẻ thư viện.");
        }

        return null;
    }

    private async Task<string> GenerateUniqueCardNumberAsync(string readerTypeCode, int year, CancellationToken ct)
    {
        var cardPrefix = readerTypeCode.ToUpperInvariant() switch
        {
            "LECTURER" => LibraryConstants.CardPrefix.Staff,
            "GUEST" => LibraryConstants.CardPrefix.Guest,
            _ => LibraryConstants.CardPrefix.Student
        };

        var sequence = await unitOfWork.Readers.CountAsync(cancellationToken: ct) + 1;
        string cardNo;
        do
        {
            cardNo = $"{cardPrefix}{year}{sequence:D5}";
            sequence++;
        }
        while (await unitOfWork.Readers.ExistsAsync(x => x.CardNo == cardNo, ct));

        return cardNo;
    }

    private static string FormatAccountLockedMessage(DateTimeOffset lockedUntil, DateTimeOffset now)
    {
        var remainingMinutes = (int)Math.Ceiling((lockedUntil - now).TotalMinutes);
        return ErrorMessages.Format(ErrorCode.AuthAccountLocked, remainingMinutes, DateTimeHelper.ToVietnamTimeString(lockedUntil));
    }

    private static string FormatReaderBlockedMessage(Reader reader) =>
        reader.CardStatus == LibraryConstants.CardStatus.Locked
            ? ErrorMessages.Format(ErrorCode.AuthCardLocked, reader.LockReason ?? "Liên hệ thủ thư để được hỗ trợ")
            : ErrorMessages.Get(ErrorCode.AuthAccountInactive);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
