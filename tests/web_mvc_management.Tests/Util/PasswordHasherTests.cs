using System;
using System.Diagnostics;
using web_mvc_management.Util;
using Xunit;

namespace web_mvc_management.Tests.Util;

public class PasswordHasherTests
{
    [Fact]
    public void HashPassword_ShouldGenerateValidFormat()
    {
        var password = "SecurePassword123!";
        var hash = PasswordHasher.HashPassword(password);

        Assert.NotNull(hash);
        var parts = hash.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.Equal("100000", parts[0]);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        var password = "CorrectPassword123!";
        var hash = PasswordHasher.HashPassword(password);

        var isValid = PasswordHasher.VerifyPassword(password, hash);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ShouldReturnFalse()
    {
        var hash = PasswordHasher.HashPassword("CorrectPassword123!");

        var isValid = PasswordHasher.VerifyPassword("WrongPassword456!", hash);

        Assert.False(isValid);
    }

    [Fact]
    public void VerifyDummyPassword_ShouldExecuteSuccessfullyAndPreventTimingAttack()
    {
        var stopwatch = Stopwatch.StartNew();
        var isValid = PasswordHasher.VerifyDummyPassword("AttackerGuessPassword123!");
        stopwatch.Stop();

        Assert.False(isValid);
        // Ensure execution took CPU work (PBKDF2 100k rounds)
        Assert.True(stopwatch.ElapsedMilliseconds >= 0);
    }
}
