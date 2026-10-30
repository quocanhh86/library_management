using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using web_mvc_management.Constant;
using web_mvc_management.Data;
using web_mvc_management.Entity;
using web_mvc_management.Infrastructure;

namespace web_mvc_management.Services;

/// <summary>
/// Triển khai dịch vụ OTP Engine chuẩn an ninh:
/// - SEC-04: OTP Verification Brute-Force Defense & OTP Burning Engine (giới hạn 5 lần sai).
/// - SEC-08: Persistent Database Storage (lưu SQL Server, an toàn cụm phân tán).
/// - SEC-12: OTP Purpose & Namespace Isolation.
/// - SEC-14: Concurrency Race Condition Defense.
/// </summary>
public class OtpService(ApplicationDbContext context, IDateTimeProvider dateTimeProvider) : IOtpService
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan DefaultValidity = TimeSpan.FromMinutes(5);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> ConcurrencyLocks = new();

    public async Task<string> GenerateOtpAsync(string email, string purpose, TimeSpan? validity = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var now = dateTimeProvider.UtcNow;
        var expiresAt = now.Add(validity ?? DefaultValidity);

        // Vô hiệu hóa mọi OTP còn hiệu lực trước đó của email và purpose này (chống Replay)
        var oldOtps = await context.Otps
            .Where(x => x.Email == normalizedEmail && x.Purpose == purpose && !x.IsUsed && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var old in oldOtps)
        {
            old.IsUsed = true;
        }

        // Sinh 6 chữ số ngẫu nhiên an toàn mã hóa (100000 -> 999999)
        var codeNumber = RandomNumberGenerator.GetInt32(100000, 1000000);
        var otpCode = codeNumber.ToString();

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

        await context.Otps.AddAsync(otpEntry, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return otpCode;
    }

    public async Task<OtpVerificationResult> VerifyOtpAsync(string email, string purpose, string otpCode, CancellationToken cancellationToken = default)
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
            // Tìm bản ghi OTP mới nhất chưa sử dụng cho email và purpose
            var entry = await context.Otps
                .Where(x => x.Email == normalizedEmail && x.Purpose == purpose && !x.IsUsed)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (entry == null)
            {
                return OtpVerificationResult.NotFound(ErrorMessages.Get(ErrorCode.OtpNotFound));
            }

            // Kiểm tra quá hạn
            if (entry.ExpiresAt <= now)
            {
                entry.IsUsed = true;
                await context.SaveChangesAsync(cancellationToken);
                return OtpVerificationResult.Expired(ErrorMessages.Get(ErrorCode.OtpExpired));
            }

            // Kiểm tra mã OTP đúng hay sai
            if (CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(entry.OtpCode),
                    System.Text.Encoding.UTF8.GetBytes(normalizedCode)))
            {
                // SEC-04: Đúng mã -> Đánh dấu đã dùng ngay lập tức chống tấn công phát lại (Replay Attack)
                entry.IsUsed = true;
                await context.SaveChangesAsync(cancellationToken);
                return OtpVerificationResult.Success();
            }

            // Sai mã -> Tăng số lần thử sai
            entry.FailedAttempts++;

            // SEC-04: Đạt ngưỡng 5 lần sai -> ĐỐT HỦY MÃ (BURN OTP) NGAY LẬP TỨC
            if (entry.FailedAttempts >= MaxFailedAttempts)
            {
                entry.IsUsed = true; // Vô hiệu hóa vĩnh viễn
                await context.SaveChangesAsync(cancellationToken);
                return OtpVerificationResult.Burned(
                    ErrorMessages.Format(ErrorCode.OtpBurned, MaxFailedAttempts));
            }

            await context.SaveChangesAsync(cancellationToken);
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
        var otps = await context.Otps
            .Where(x => x.Email == normalizedEmail && x.Purpose == purpose && !x.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var otp in otps)
        {
            otp.IsUsed = true;
        }

        if (otps.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
