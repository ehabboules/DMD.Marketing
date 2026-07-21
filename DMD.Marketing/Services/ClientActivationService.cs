using DMD.Marketing.Data;
using DMD.Marketing.Models;
using Microsoft.EntityFrameworkCore;

namespace DMD.Marketing.Services;

// ── Result model ────────────────────────────────────────────────────────────

public record ActivationResult(
    int    UserId,
    string Slug,
    string AppUrl,
    DateTime ExpiresAt,
    bool   WelcomeEmailSent,
    string? Error = null)
{
    public bool Succeeded => Error is null;
}

// ── Service ─────────────────────────────────────────────────────────────────

public class ClientActivationService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly ProvisioningService _provisioning;
    private readonly EmailService        _email;
    private readonly IConfiguration      _config;
    private readonly ILogger<ClientActivationService> _logger;

    private const int DefaultTrialDays = 30;

    public ClientActivationService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        ProvisioningService provisioning,
        EmailService email,
        IConfiguration config,
        ILogger<ClientActivationService> logger)
    {
        _dbFactory    = dbFactory;
        _provisioning = provisioning;
        _email        = email;
        _config       = config;
        _logger       = logger;
    }

    public async Task<ActivationResult> ActivateAsync(
        int userId, string slug, string tempPassword, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return new ActivationResult(userId, slug, "", DateTime.UtcNow, false,
                $"User {userId} not found.");

        // Build AppUrl from config template or default pattern
        var baseUrlTemplate = _config["Provisioning:AppUrlTemplate"] ?? "{slug}.cloudmpos.com";
        var appUrl = baseUrlTemplate.Replace("{slug}", slug.ToLowerInvariant().Trim());

        // Trial days from config or default
        var trialDays = int.TryParse(_config["Provisioning:TrialDays"], out var td) ? td : DefaultTrialDays;
        var expiresAt = DateTime.UtcNow.AddDays(trialDays);

        // Update the user record
        user.ActivationStatus      = ActivationStatus.Active;
        user.AppUrl                = appUrl;
        user.TenantSlug            = slug.ToLowerInvariant().Trim();
        user.SubscriptionExpiresAt = expiresAt;
        user.ModifiedAt            = DateTime.UtcNow;
        user.ModifiedBy            = "provisioning-wizard";

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} activated: slug={Slug}, appUrl={AppUrl}, expires={ExpiresAt}",
            userId, slug, appUrl, expiresAt);

        // Notify StockShopOnline (best-effort — don't fail activation if this errors)
        try
        {
            await _provisioning.ActivateAsync(user.Email, slug, expiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify StockShopOnline about activation for user {UserId}", userId);
        }

        // Push initial license snapshot into the tenant DB (best-effort)
        // Timezone, taxes, business type are seed-only — tenant edits these in POS System Settings after activation
        try
        {
            await _provisioning.PushLicenseAsync(
                slug,
                planSlug:     user.SelectedPlan.ToString().ToLowerInvariant(),
                billingCycle: user.BillingCycle.ToString(),
                status:       "Active",
                activatedAt:  DateTime.UtcNow,
                expiresAt:    expiresAt,
                appUrl:       appUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push license snapshot for user {UserId} slug {Slug}", userId, slug);
        }

        // Send welcome email (best-effort)
        var emailSent = false;
        try
        {
            emailSent = await _email.SendWelcomeEmailAsync(user, appUrl, tempPassword);
            if (!emailSent)
                _logger.LogWarning("Welcome email returned false for user {UserId} — check Resend config", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send welcome email for user {UserId}", userId);
        }

        return new ActivationResult(userId, slug, appUrl, expiresAt, emailSent);
    }
}
