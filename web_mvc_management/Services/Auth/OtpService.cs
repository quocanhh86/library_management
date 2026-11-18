using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Infrastructure;
using web_mvc_management.Repositories;

namespace web_mvc_management.Services;

/// <summary>
/// Triển khai dịch vụ OTP Engine chuẩn an ninh:
/// SEC-04 (5 lần sai đốt mã), SEC-08 (lưu DB bền vững), SEC-12 (Namespace Isolation), SEC-14 (Chống Race Condition).
/// </summary>
public class OtpService(IUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider) : IOtpService
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan DefaultValidity = TimeSpan.FromMinutes(5);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ConcurrencyLocks = new();

    public async Task<string> GenerateOtpAsync(
        string email,
        string purpose,
        TimeSpan? validity = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var now = dateTimeProvider.UtcNow;
        var expiresAt = now.Add(validity ?? DefaultValidity);

        var oldOtps = await unitOfWork.Otps.FindAsync(
            x => x.Email == normalizedEmail && x.Purpose == purpose && !x.IsUsed && x.ExpiresAt > now,
            asNoTracking: false,
            cancellationToken: cancellationToken);

        foreach (var old in oldOtps)
        {
            old.IsUsed = true;
        }

        var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        var otpEntry = new OtpEntry
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            OtpCode = otpCode,
            Purpose = purpose,
            FailedAttempts = 0,
            IsUsed = false,
            ExpiresAt = expiresAt,
            CreatedAt = now
        };

        await unitOfWork.Otps.AddAsync(otpEntry, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return otpCode;
    }

    public async Task<OtpVerificationResult> VerifyOtpAsync(
        string email,
        string purpose,
        string otpCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otpCode))
        {
            return OtpVerificationResult.NotFound(ErrorMessages.Get(ErrorCode.OtpInfoIncomplete));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedCode = otpCode.Trim();
        var now = dateTimeProvider.UtcNow;

        var lockKey = $"{normalizedEmail}:{purpose}";
        var semaphore = ConcurrencyLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            var entry = await unitOfWork.Otps
                .GetQueryable(asNoTracking: false)
                .Where(x => x.Email == normalizedEmail && x.Purpose == purpose && !x.IsUsed)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (entry == null)
            {
                return OtpVerificationResult.NotFound(ErrorMessages.Get(ErrorCode.OtpNotFound));
            }

            if (entry.ExpiresAt <= now)
            {
                entry.IsUsed = true;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return OtpVerificationResult.Expired(ErrorMessages.Get(ErrorCode.OtpExpired));
            }

            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(entry.OtpCode),
                    Encoding.UTF8.GetBytes(normalizedCode)))
            {
                entry.IsUsed = true;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return OtpVerificationResult.Success();
            }

            entry.FailedAttempts++;

            if (entry.FailedAttempts >= MaxFailedAttempts)
            {
                entry.IsUsed = true;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return OtpVerificationResult.Burned(ErrorMessages.Format(ErrorCode.OtpBurned, MaxFailedAttempts));
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            var remaining = MaxFailedAttempts - entry.FailedAttempts;
            return OtpVerificationResult.Incorrect(remaining, ErrorMessages.Format(ErrorCode.OtpInvalid, remaining));
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task InvalidateOtpAsync(string email, string purpose, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var otps = await unitOfWork.Otps.FindAsync(
            x => x.Email == normalizedEmail && x.Purpose == purpose && !x.IsUsed,
            asNoTracking: false,
            cancellationToken: cancellationToken);

        foreach (var otp in otps)
        {
            otp.IsUsed = true;
        }

        if (otps.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
