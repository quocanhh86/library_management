using System.Security.Cryptography;

namespace web_mvc_management.Util;

/// <summary>
/// Hỗ trợ băm mật khẩu bảo mật chuẩn PBKDF2 (HMAC-SHA256) kèm Salt ngẫu nhiên.
/// Tích hợp cơ chế phòng thủ tấn công kênh phụ (SEC-15: Authentication Side-Channel Timing Attacks Defense).
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16; // 128-bit
    private const int KeySize = 32;  // 256-bit
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>
    /// Hash giả lập dùng để chạy kiểm tra mật khẩu khi tài khoản không tồn tại,
    /// đảm bảo thời gian xử lý luôn đồng nhất ~100-200ms, chống lại Timing Attack (SEC-15).
    /// </summary>
    private static readonly string DummyHash = HashPassword("UtcLibrary#SecurityDummyPassword2026!");

    /// <summary>
    /// Băm mật khẩu ra chuỗi định dạng: {iterations}.{salt_base64}.{hash_base64}
    /// </summary>
    public static string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            Algorithm,
            KeySize
        );

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Kiểm tra mật khẩu người dùng nhập có khớp với chuỗi băm hay không.
    /// </summary>
    public static bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedHash = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            Algorithm,
            expectedHash.Length
        );

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    /// <summary>
    /// Chạy hàm băm giả lập khi username hoặc email không tồn tại trong CSDL.
    /// Triệt tiêu hoàn toàn khả năng đo độ trễ mạng để phát hiện tài khoản tồn tại (SEC-15).
    /// </summary>
    public static bool VerifyDummyPassword(string password) => VerifyPassword(password, DummyHash);
}
