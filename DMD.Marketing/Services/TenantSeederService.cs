namespace DMD.Marketing.Services;

// ── Result model ─────────────────────────────────────────────────────────────

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

    /// <summary>Production seed — creates only screen roles, warehouse, and the client's admin account.</summary>
    public async Task<SeedStep> SeedAsync(
        string slug, string connectionString,
        string email, string firstName, string lastName, string tempPassword,
        CancellationToken ct)
    {
        _logger.LogInformation("Production-seeding tenant '{Slug}' for {Email}…", slug, email);

        var (registered, registerError) = await _provisioning.RegisterTenantAsync(slug, connectionString);
        if (!registered)
        {
            var err = $"Registration failed before seeding: {registerError}";
            _logger.LogError("{Error}", err);
            return new SeedStep(slug, "Production", "RegistrationFailed", err);
        }

        ct.ThrowIfCancellationRequested();

        try
        {
            var ok = await _provisioning.SeedTenantAsync(
                slug, profile: null,
                email: email, firstName: firstName, lastName: lastName, tempPassword: tempPassword);

            if (ok)
            {
                _logger.LogInformation("Tenant '{Slug}' production-seeded for {Email}", slug, email);
                return new SeedStep(slug, "Production", "Seeded");
            }

            var error = $"Production seed returned false for '{slug}'.";
            _logger.LogError("{Error}", error);
            return new SeedStep(slug, "Production", "Failed", error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Production seed failed for tenant '{Slug}'", slug);
            return new SeedStep(slug, "Production", "Failed", ex.Message);
        }
    }
}
