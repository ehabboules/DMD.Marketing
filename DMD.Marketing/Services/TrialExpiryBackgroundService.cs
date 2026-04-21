using DMD.Marketing.Data;
using DMD.Marketing.Models;
using Microsoft.EntityFrameworkCore;

namespace DMD.Marketing.Services;

public class TrialExpiryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<TrialExpiryBackgroundService> _logger;

    public TrialExpiryBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<TrialExpiryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in trial expiry background service");
            }

            // Run once daily at ~6 AM UTC
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1).AddHours(6);
            var delay = nextRun - now;
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task SendRemindersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

        var today = DateTime.UtcNow.Date;
        var threeDaysFromNow = today.AddDays(3);

        // Find users whose trial expires in exactly 3 days or today (expiry day)
        var usersToNotify = await db.Users
            .Where(u => u.ActivationStatus == ActivationStatus.Pending
                && u.SubscriptionExpiresAt != null
                && (u.SubscriptionExpiresAt.Value.Date == threeDaysFromNow
                    || u.SubscriptionExpiresAt.Value.Date == today))
            .ToListAsync(ct);

        if (usersToNotify.Count == 0)
        {
            _logger.LogInformation("No trial expiry reminders to send today");
            return;
        }

        _logger.LogInformation("Sending trial expiry reminders to {Count} users", usersToNotify.Count);

        var baseUrl = _config["BaseUrl"] ?? "https://dmd-inventory.com";
        var paymentUrl = $"{baseUrl.TrimEnd('/')}/payment";

        foreach (var user in usersToNotify)
        {
            await emailService.SendTrialExpiryReminderAsync(
                user.Email,
                user.FirstName ?? "there",
                paymentUrl,
                user.SubscriptionExpiresAt!.Value);
        }
    }
}
