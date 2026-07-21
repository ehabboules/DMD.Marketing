namespace DMD.Marketing.Services;

// ── Enum & Result model ─────────────────────────────────────────────────────

public enum SeedProfile
{
    GenericStore,
    Restaurant,
    MobileShop,
    GroceryNuts
}

public record SeedStep(string Slug, string Profile, string Status, string? Error = null)
{
    public bool Succeeded => Status == "Seeded";
}

// ── Service ─────────────────────────────────────────────────────────────────

/// <summary>
/// Seeds a freshly migrated tenant database via StockShopOnline's
/// POST /api/provisioning/tenants/{slug}/seed endpoint.
/// StockShopOnline's DataSeeder handles: default roles, screen-role permissions,
/// admin user, default warehouse, system settings, and sample data per profile.
/// </summary>
public class TenantSeederService
{
    private readonly ProvisioningService _provisioning;
    private readonly ILogger<TenantSeederService> _logger;

    public TenantSeederService(
        ProvisioningService provisioning,
        ILogger<TenantSeederService> logger)
    {
        _provisioning = provisioning;
        _logger       = logger;
    }

    public async Task<SeedStep> SeedAsync(
        string slug, string connectionString, SeedProfile profile, CancellationToken ct)
    {
        var profileName = profile.ToString(); // "GenericStore", "Restaurant", etc.

        _logger.LogInformation("Seeding tenant '{Slug}' with profile '{Profile}'…", slug, profileName);

        // The tenant must already be registered (TenantMigrationService does this).
        // If not, registration is idempotent — register again just in case.
        var (registered, registerError) = await _provisioning.RegisterTenantAsync(slug, connectionString);
        if (!registered)
        {
            var err = $"Registration failed before seeding: {registerError}";
            _logger.LogError("{Error}", err);
            return new SeedStep(slug, profileName, "RegistrationFailed", err);
        }

        ct.ThrowIfCancellationRequested();

        try
        {
            var ok = await _provisioning.SeedTenantAsync(slug, profileName);
            if (ok)
            {
                _logger.LogInformation("Tenant '{Slug}' seeded with profile '{Profile}'", slug, profileName);
                return new SeedStep(slug, profileName, "Seeded");
            }

            var error = $"SeedTenantAsync returned false for '{slug}' with profile '{profileName}'.";
            _logger.LogError("{Error}", error);
            return new SeedStep(slug, profileName, "Failed", error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Seeding failed for tenant '{Slug}'", slug);
            return new SeedStep(slug, profileName, "Failed", ex.Message);
        }
    }
}
