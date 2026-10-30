using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using web_mvc_management.Infrastructure;
using web_mvc_management.Services;
using Xunit;

namespace web_mvc_management.Tests.Services;

public class MockHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "web_mvc_management";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
}

public class EmailServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly MockHostEnvironment _environment;

    public EmailServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["MailSettings:Host"] = "smtp.gmail.com",
            ["MailSettings:Port"] = "587",
            ["MailSettings:Username"] = "",
            ["MailSettings:Password"] = "",
            ["MailSettings:SenderDisplayName"] = "Thư Viện UTC"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _environment = new MockHostEnvironment();
    }

    [Fact]
    public async Task SendOtpEmailAsync_FirstTime_ShouldSucceed()
    {
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var emailService = new EmailService(_configuration, timeProvider, NullLogger<EmailService>.Instance, _environment);

        var testEmail = $"user_{Guid.NewGuid():N}@utc.edu.vn";
        var result = await emailService.SendOtpEmailAsync(testEmail, "123456", "REGISTER");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task SendOtpEmailAsync_WithinCooldownPeriod_ShouldFail_SEC17()
    {
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var emailService = new EmailService(_configuration, timeProvider, NullLogger<EmailService>.Instance, _environment);

        var testEmail = $"user_{Guid.NewGuid():N}@utc.edu.vn";

        // First send
        var firstResult = await emailService.SendOtpEmailAsync(testEmail, "123456", "REGISTER");
        Assert.True(firstResult.Succeeded);

        // Immediate second send (10 seconds later)
        timeProvider.UtcNow = timeProvider.UtcNow.AddSeconds(10);
        var remainingSeconds = emailService.GetRemainingCooldownSeconds(testEmail);
        Assert.True(remainingSeconds > 0);

        var secondResult = await emailService.SendOtpEmailAsync(testEmail, "654321", "REGISTER");
        Assert.False(secondResult.Succeeded);
        Assert.Contains("quá nhanh", secondResult.Message);
    }

    [Fact]
    public async Task SendOtpEmailAsync_AfterCooldownExpires_ShouldSucceedAgain()
    {
        var timeProvider = new TestDateTimeProvider(DateTimeOffset.UtcNow);
        var emailService = new EmailService(_configuration, timeProvider, NullLogger<EmailService>.Instance, _environment);

        var testEmail = $"user_{Guid.NewGuid():N}@utc.edu.vn";

        await emailService.SendOtpEmailAsync(testEmail, "123456", "REGISTER");

        // Advance time 65 seconds
        timeProvider.UtcNow = timeProvider.UtcNow.AddSeconds(65);
        Assert.Equal(0, emailService.GetRemainingCooldownSeconds(testEmail));

        var secondResult = await emailService.SendOtpEmailAsync(testEmail, "999888", "REGISTER");
        Assert.True(secondResult.Succeeded);
    }
}
