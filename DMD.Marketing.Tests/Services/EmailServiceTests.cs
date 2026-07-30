using Microsoft.Extensions.Configuration;

namespace DMD.Marketing.Tests.Services;

public class EmailServiceTests
{
    private static EmailService CreateService(Dictionary<string, string?> configValues)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        return new EmailService(config, httpFactory.Object, NullLogger<EmailService>.Instance);
    }

    // ── Config validation ───────────────────────────────────────────────────

    [Fact]
    public async Task SendDemoRequest_ReturnsFalse_WhenApiKeyMissing()
    {
        var svc = CreateService(new()
        {
            ["SendGrid:ApiKey"]   = null,
            ["SendGrid:ToEmail"]  = "to@example.com",
            ["SendGrid:FromEmail"]= "from@example.com",
            ["SendGrid:FromName"] = "Test",
        });
        var model = MakeModel();

        var result = await svc.SendDemoRequestAsync(model);

        Assert.False(result);
    }

    [Fact]
    public async Task SendDemoRequest_ReturnsFalse_WhenToEmailMissing()
    {
        var svc = CreateService(new()
        {
            ["SendGrid:ApiKey"]   = "SG.fake",
            ["SendGrid:ToEmail"]  = null,
            ["SendGrid:FromEmail"]= "from@example.com",
            ["SendGrid:FromName"] = "Test",
        });
        var model = MakeModel();

        var result = await svc.SendDemoRequestAsync(model);

        Assert.False(result);
    }

    [Fact]
    public async Task SendPasswordReset_ReturnsFalse_WhenApiKeyMissing()
    {
        var svc = CreateService(new()
        {
            ["SendGrid:ApiKey"]   = null,
            ["SendGrid:FromEmail"]= "from@example.com",
        });

        var result = await svc.SendPasswordResetAsync("to@example.com", "Alice", "https://example.com/reset");

        Assert.False(result);
    }

    [Fact]
    public async Task SendTrialExpiryReminder_ReturnsFalse_WhenApiKeyMissing()
    {
        var svc = CreateService(new()
        {
            ["SendGrid:ApiKey"]   = null,
            ["SendGrid:FromEmail"]= "from@example.com",
        });

        var result = await svc.SendTrialExpiryReminderAsync(
            "to@example.com", "Bob", "https://example.com/pay", DateTime.UtcNow.AddDays(3));

        Assert.False(result);
    }

    // ── Days-remaining calculation (observable via no-crash) ────────────────

    [Fact]
    public async Task SendTrialExpiryReminder_ReturnsFalse_AndDoesNotThrow_WhenAlreadyExpired()
    {
        var svc = CreateService(new() { ["SendGrid:ApiKey"] = null });
        var pastExpiry = DateTime.UtcNow.AddDays(-1);

        // Should return false without throwing
        var result = await svc.SendTrialExpiryReminderAsync("e@e.com", "X", "http://x.com", pastExpiry);

        Assert.False(result);
    }

    // ── Helper ──────────────────────────────────────────────────────────────

    private static ContactFormModel MakeModel() => new()
    {
        Name         = "Alice",
        Email        = "alice@example.com",
        Company      = "ACME",
        BusinessType = "General Retail",
        Message      = "Hello",
    };
}
