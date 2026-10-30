using System;
using web_mvc_management.Services;
using Xunit;

namespace web_mvc_management.Tests.Services;

public class IpRateLimiterTests
{
    [Fact]
    public void IsAllowed_WhenUnderLimit_ShouldReturnTrue()
    {
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new IpRateLimiterService(timeProvider);

        for (int i = 0; i < 5; i++)
        {
            var allowed = limiter.IsAllowed("192.168.1.100", "SEND_OTP", 5);
            Assert.True(allowed);
        }
    }

    [Fact]
    public void IsAllowed_WhenExceedingLimit_ShouldReturnFalse_SEC17()
    {
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new IpRateLimiterService(timeProvider);

        for (int i = 0; i < 5; i++)
        {
            limiter.IsAllowed("10.0.0.1", "LOGIN", 5);
        }

        // 6th attempt in same minute must be blocked
        var blocked = limiter.IsAllowed("10.0.0.1", "LOGIN", 5);
        Assert.False(blocked);
    }

    [Fact]
    public void IsAllowed_AfterOneMinute_ShouldAllowAgain()
    {
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new IpRateLimiterService(timeProvider);

        for (int i = 0; i < 5; i++)
        {
            limiter.IsAllowed("10.0.0.2", "VERIFY_OTP", 5);
        }

        Assert.False(limiter.IsAllowed("10.0.0.2", "VERIFY_OTP", 5));

        // Advance 61 seconds
        timeProvider.UtcNow = timeProvider.UtcNow.AddSeconds(61);

        Assert.True(limiter.IsAllowed("10.0.0.2", "VERIFY_OTP", 5));
    }
}
