namespace DMD.Marketing.Services;

// ── Result model ────────────────────────────────────────────────────────────

public record MigrationStep(string Slug, string ConnectionString, string Status, string? Error = null)
{
    public bool Succeeded => Status == "Migrated";
}

// ── Service ─────────────────────────────────────────────────────────────────

/// <summary>
/// Runs StockShopOnline EF Core migrations against a freshly provisioned tenant database.
/// Delegates to StockShopOnline's POST /api/provisioning/tenants/{slug}/onboard endpoint
/// (which calls TenantOnboardingService.OnboardAsync internally) because DMD.Marketing
/// and DMD.StockShopOnline are separate solutions with incompatible OpenIddict versions
/// (5.4 vs 7.2) — a direct project reference would cause NuGet conflicts.
///
/// Includes retry logic with exponential backoff because fresh Railway DBs sometimes
/// take a few seconds to accept connections.
/// </summary>
public class TenantMigrationService
{
    private readonly ProvisioningService _provisioning;
    private readonly ILogger<TenantMigrationService> _logger;

    private const int MaxRetries = 3;

    public TenantMigrationService(
        ProvisioningService provisioning,
        ILogger<TenantMigrationService> logger)
    {
        _provisioning = provisioning;
        _logger       = logger;
    }

    /// <summary>
    /// Ensures the tenant is registered in StockShopOnline, then runs EF Core migrations
    /// with up to 3 retries and exponential backoff.
    /// </summary>
    public async Task<MigrationStep> MigrateAsync(
        string slug, string connectionString, CancellationToken ct)
    {
        // 1. Register the tenant (idempotent — StockShopOnline overwrites if slug already exists)
        _logger.LogInformation("Registering tenant '{Slug}' before migration…", slug);
        var registered = await _provisioning.RegisterTenantAsync(slug, connectionString);
        if (!registered)
        {
            var err = $"Failed to register tenant '{slug}' in StockShopOnline — cannot run migrations.";
            _logger.LogError("{Error}", err);
            return new MigrationStep(slug, MaskConnectionString(connectionString), "RegistrationFailed", err);
        }

        // 2. Run migrations with retry + exponential backoff
        Exception? lastEx = null;
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            _logger.LogInformation("Running migrations for tenant '{Slug}' (attempt {Attempt}/{Max})…",
                slug, attempt, MaxRetries);

            try
            {
                var ok = await _provisioning.OnboardTenantAsync(slug);
                if (ok)
                {
                    _logger.LogInformation("Migrations completed for tenant '{Slug}'", slug);
                    return new MigrationStep(slug, MaskConnectionString(connectionString), "Migrated");
                }

                lastEx = new InvalidOperationException(
                    $"OnboardTenantAsync returned false for '{slug}' on attempt {attempt}.");
            }
            catch (Exception ex)
            {
                lastEx = ex;
                _logger.LogWarning(ex, "Migration attempt {Attempt}/{Max} failed for tenant '{Slug}'",
                    attempt, MaxRetries, slug);
            }

            if (attempt < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s, 8s
                _logger.LogInformation("Retrying in {Delay}s…", delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }

        var error = lastEx?.Message ?? "Migration failed after all retries.";
        _logger.LogError("All {Max} migration attempts failed for tenant '{Slug}': {Error}",
            MaxRetries, slug, error);
        return new MigrationStep(slug, MaskConnectionString(connectionString), "Failed", error);
    }

    private static string MaskConnectionString(string cs)
    {
        // Only expose Host for logging — never log passwords
        var parts = cs.Split(';');
        var host = Array.Find(parts, p => p.TrimStart().StartsWith("Host=", StringComparison.OrdinalIgnoreCase));
        return host?.Trim() ?? "(masked)";
    }
}
