using System.Net.Http.Json;
using DMD.Marketing.Models;

namespace DMD.Marketing.Services;

// ── Response models ──────────────────────────────────────────────────
public record LicensePaymentInfo(
    int       Id,
    string    PlanSlug,
    string    BillingCycle,
    decimal   Amount,
    string    Currency,
    DateTime  PaidAt,
    DateTime  PeriodStart,
    DateTime  PeriodEnd,
    string?   PaymentMethod,
    string?   InvoiceNumber,
    string?   Notes
);

public record ClientInfo(
    int       Id,
    string    Email,
    string    FirstName,
    string    LastName,
    string    StoreName,
    string?   StorePhone,
    string?   StoreTimezone,
    string?   BusinessType,
    string    PlanSlug,
    string    BillingCycle,
    string?   Currency,
    decimal   FederalTaxRate,
    decimal   ProvincialTaxRate,
    bool      TaxInclusive,
    string    Status,
    string?   AppUrl,
    DateTime  RequestedAt,
    DateTime? ExpiresAt,
    DateTime? ProvisionedAt,
    string?   Notes,
    int?      MarketingUserId,
    List<LicensePaymentInfo> Payments
);

public record ProvisioningRequestInfo(
    int       Id,
    string    Email,
    string    FirstName,
    string    LastName,
    string    StoreName,
    string?   StorePhone,
    string?   StoreTimezone,
    string?   BusinessType,
    string    PlanSlug,
    string    BillingCycle,
    string?   Currency,
    decimal   FederalTaxRate,
    decimal   ProvincialTaxRate,
    bool      TaxInclusive,
    string    Status,
    string?   AppUrl,
    DateTime  RequestedAt,
    DateTime? ExpiresAt,
    DateTime? ProvisionedAt,
    string?   Notes,
    int?      MarketingUserId
);

public record PosAuditEntry(string Action, string? Detail, DateTime CreatedAt);

public class ProvisioningService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration     _config;
    private readonly ILogger<ProvisioningService> _logger;

    public ProvisioningService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<ProvisioningService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config            = config;
        _logger            = logger;
    }

    // ── shared helper — 20 s timeout on every outbound call ───────────
    private (HttpClient? client, string? baseUrl) GetClient()
    {
        var baseUrl = _config["Provisioning:StockShopBaseUrl"];
        var apiKey  = _config["Provisioning:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Provisioning:StockShopBaseUrl or Provisioning:ApiKey not configured.");
            return (null, null);
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return (client, baseUrl.TrimEnd('/'));
    }

    // ── Demo tenant tools ─────────────────────────────────────────────
    public async Task<bool> SeedDemoTenantAsync(string slug)
    {
        var (client, baseUrl) = GetClient();
        if (client is null) return false;

        try
        {
            var response = await client.PostAsync(
                $"{baseUrl}/admin/seed?tenant={Uri.EscapeDataString(slug)}", null);
            if (response.IsSuccessStatusCode) return true;
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("SeedTenant failed: {Status} — {Body}", response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding tenant {Slug}", slug);
            return false;
        }
    }

    public async Task<bool> RunMigrationsAsync()
    {
        var (client, baseUrl) = GetClient();
        if (client is null) return false;

        try
        {
            var response = await client.PostAsync($"{baseUrl}/admin/onboard", null);
            if (response.IsSuccessStatusCode) return true;
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("RunMigrations failed: {Status} — {Body}", response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running migrations");
            return false;
        }
    }

    // ── Write ─────────────────────────────────────────────────────────
    public async Task<bool> RecordPaymentAsync(
        string   email,
        string   planSlug,
        string   billingCycle,
        decimal  amount,
        string   currency,
        DateTime paidAt,
        DateTime periodStart,
        DateTime periodEnd,
        string?  paymentMethod,
        string?  invoiceNumber,
        string?  notes = null)
    {
        var (client, baseUrl) = GetClient();
        if (client is null) return false;

        var payload = new
        {
            planSlug, billingCycle, amount, currency,
            paidAt, periodStart, periodEnd,
            paymentMethod, invoiceNumber, notes
        };

        try
        {
            var encodedEmail = Uri.EscapeDataString(email);
            var response = await client.PostAsJsonAsync(
                $"{baseUrl}/api/provisioning/payments/{encodedEmail}", payload);

            if (response.IsSuccessStatusCode) return true;

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Record payment failed: {Status} — {Body}", response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling record payment API");
            return false;
        }
    }

    /// <summary>PATCH — only non-null fields are sent; StockShopOnline ignores nulls.</summary>
    public async Task<bool> UpdateAsync(
        string    email,
        string?   planSlug          = null,
        string?   billingCycle      = null,
        string?   storeName         = null,
        string?   storePhone        = null,
        string?   storeTimezone     = null,
        string?   businessType      = null,
        string?   currency          = null,
        decimal?  federalTaxRate    = null,
        decimal?  provincialTaxRate = null,
        bool?     taxInclusive      = null,
        DateTime? expiresAt         = null,
        string?   appUrl            = null,
        string?   notes             = null,
        string?   status            = null)
    {
        var (client, baseUrl) = GetClient();
        if (client is null) return false;

        var payload = new
        {
            planSlug, billingCycle,
            storeName, storePhone, storeTimezone, businessType,
            currency, federalTaxRate, provincialTaxRate, taxInclusive,
            expiresAt, appUrl, notes, status
        };

        try
        {
            var encodedEmail = Uri.EscapeDataString(email);
            var response = await client.PatchAsJsonAsync(
                $"{baseUrl}/api/provisioning/request/{encodedEmail}", payload);

            if (response.IsSuccessStatusCode) return true;

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Provisioning update failed: {Status} — {Body}", response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling provisioning update API");
            return false;
        }
    }

    public async Task<bool> ActivateAsync(string email, string slug, DateTime? expiresAt = null)
    {
        var (client, baseUrl) = GetClient();
        if (client is null) return false;

        try
        {
            var encodedEmail = Uri.EscapeDataString(email);
            var response = await client.PostAsJsonAsync(
                $"{baseUrl}/api/provisioning/activate/{encodedEmail}",
                new { slug, expiresAt });

            if (response.IsSuccessStatusCode) return true;

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Activate failed: {Status} — {Body}", response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling activate API for {Email}", email);
            return false;
        }
    }

    // ── Tenant registration ──────────────────────────────────────────

    /// <summary>
    /// Registers a tenant's connection string in StockShopOnline so it can serve requests for that subdomain.
    /// </summary>
    public async Task<(bool Success, string? Error)> RegisterTenantAsync(
        string slug, string connectionString, string? storeName = null,
        string? businessType = null, string? seedProfile = null)
    {
        var (client, baseUrl) = GetClient();
        if (client is null)
            return (false, "Provisioning not configured — check Provisioning:StockShopBaseUrl and Provisioning:ApiKey.");

        var payload = new { slug, connectionString, storeName, businessType, seedProfile };

        try
        {
            var response = await client.PostAsJsonAsync(
                $"{baseUrl}/api/provisioning/tenants", payload);

            if (response.IsSuccessStatusCode) return (true, null);

            var body = await response.Content.ReadAsStringAsync();
            var err  = $"HTTP {(int)response.StatusCode} from StockShopOnline: {body}";
            _logger.LogError("RegisterTenant failed: {Status} — {Body}", response.StatusCode, body);
            return (false, err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling RegisterTenant API for {Slug}", slug);
            return (false, ex.Message);
        }
    }

    /// <summary>Runs EF Core migrations on a single tenant database.</summary>
    public async Task<bool> OnboardTenantAsync(string slug)
    {
        var (client, baseUrl) = GetClient();
        if (client is null) return false;

        try
        {
            var encodedSlug = Uri.EscapeDataString(slug);
            var response = await client.PostAsync(
                $"{baseUrl}/api/provisioning/tenants/{encodedSlug}/onboard", null);

            if (response.IsSuccessStatusCode) return true;

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("OnboardTenant failed: {Status} — {Body}", response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OnboardTenant API for {Slug}", slug);
            return false;
        }
    }

    /// <summary>
    /// Seeds a single tenant database.
    /// When client details are provided → production seed (structural data + client admin only).
    /// Otherwise → demo seed (backward compat for dev/admin use).
    /// </summary>
    public async Task<bool> SeedTenantAsync(
        string slug,
        string? profile = null,
        string? email = null,
        string? firstName = null,
        string? lastName = null,
        string? tempPassword = null)
    {
        var (client, baseUrl) = GetClient();
        if (client is null) return false;

        var encodedSlug = Uri.EscapeDataString(slug);

        try
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                // Production seed — POST client details as JSON body
                var payload  = new { email, firstName, lastName, temporaryPassword = tempPassword };
                var response = await client.PostAsJsonAsync(
                    $"{baseUrl}/api/provisioning/tenants/{encodedSlug}/seed", payload);

                if (response.IsSuccessStatusCode) return true;

                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("SeedTenant (production) failed: {Status} — {Body}", response.StatusCode, body);
                return false;
            }
            else
            {
                // Demo seed — no body
                var query    = !string.IsNullOrWhiteSpace(profile) ? $"?profile={Uri.EscapeDataString(profile)}" : "";
                var response = await client.PostAsync(
                    $"{baseUrl}/api/provisioning/tenants/{encodedSlug}/seed{query}", null);

                if (response.IsSuccessStatusCode) return true;

                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("SeedTenant (demo) failed: {Status} — {Body}", response.StatusCode, body);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SeedTenant API for {Slug}", slug);
            return false;
        }
    }

    // ── POS app roles / logs ─────────────────────────────────────────
    public async Task<List<string>?> GetUserPosRolesAsync(string slug, string email)
    {
        var (client, baseUrl) = GetClient();
        if (client is null) return null;

        try
        {
            var encodedEmail = Uri.EscapeDataString(email);
            var response = await client.GetAsync(
                $"{baseUrl}/api/admin/{slug}/user/{encodedEmail}/roles");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetUserPosRoles: {Status} for slug={Slug} email={Email}", response.StatusCode, slug, email);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<List<string>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching POS roles for {Email} at {Slug}", email, slug);
            return null;
        }
    }

    public async Task<List<PosAuditEntry>?> GetUserPosLogsAsync(string slug, string email)
    {
        var (client, baseUrl) = GetClient();
        if (client is null) return null;

        try
        {
            var encodedEmail = Uri.EscapeDataString(email);
            var response = await client.GetAsync(
                $"{baseUrl}/api/admin/{slug}/user/{encodedEmail}/logs");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetUserPosLogs: {Status} for slug={Slug} email={Email}", response.StatusCode, slug, email);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<List<PosAuditEntry>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching POS logs for {Email} at {Slug}", email, slug);
            return null;
        }
    }

    /// <summary>
    /// Pushes a license snapshot into the tenant's own ProvisioningRequests table via StockShopOnline.
    /// Best-effort — callers should not fail if this errors.
    /// </summary>
    public async Task<bool> PushLicenseAsync(
        string    slug,
        string    planSlug,
        string    billingCycle,
        string    status,
        DateTime? activatedAt,
        DateTime? expiresAt,
        string?   appUrl)
    {
        var (client, baseUrl) = GetClient();
        if (client is null) return false;

        var payload = new { planSlug, billingCycle, status, activatedAt, expiresAt, appUrl };

        try
        {
            var encodedSlug = Uri.EscapeDataString(slug);
            var response = await client.PutAsJsonAsync(
                $"{baseUrl}/api/provisioning/tenants/{encodedSlug}/license", payload);

            if (response.IsSuccessStatusCode) return true;

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("PushLicense failed for {Slug}: {Status} — {Body}", slug, response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing license to tenant {Slug}", slug);
            return false;
        }
    }

    public async Task<bool> RequestActivationAsync(
        string  email,
        string  firstName,
        string  lastName,
        string  planSlug,
        string  billingCycle,
        string  storeName,
        string? storePhone,
        string? storeTimezone,
        string? businessType,
        string  currency,
        decimal federalTaxRate,
        decimal provincialTaxRate,
        bool    taxInclusive,
        int?    marketingUserId)
    {
        var (client, baseUrl) = GetClient();
        if (client is null) return false;

        var payload = new
        {
            email, firstName, lastName,
            planSlug, billingCycle,
            storeName, storePhone, storeTimezone, businessType,
            currency, federalTaxRate, provincialTaxRate, taxInclusive,
            marketingUserId
        };

        try
        {
            var response = await client.PostAsJsonAsync(
                $"{baseUrl}/api/provisioning/request", payload);

            if (response.IsSuccessStatusCode) return true;

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Provisioning request failed: {Status} — {Body}", response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling provisioning API");
            return false;
        }
    }
}
