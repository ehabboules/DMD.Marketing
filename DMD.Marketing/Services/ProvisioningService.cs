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

    public async Task<List<ProvisioningRequestInfo>> GetAllRequestsAsync()
    {
        var baseUrl = _config["Provisioning:StockShopBaseUrl"];
        var apiKey  = _config["Provisioning:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Provisioning config missing.");
            return new();
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        try
        {
            var result = await client.GetFromJsonAsync<List<ProvisioningRequestInfo>>(
                $"{baseUrl.TrimEnd('/')}/api/provisioning/requests");
            return result ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching provisioning requests");
            return new();
        }
    }

    public async Task<List<ClientInfo>> GetAllClientsAsync()
    {
        var baseUrl = _config["Provisioning:StockShopBaseUrl"];
        var apiKey  = _config["Provisioning:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Provisioning config missing.");
            return new();
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        try
        {
            var result = await client.GetFromJsonAsync<List<ClientInfo>>(
                $"{baseUrl.TrimEnd('/')}/api/provisioning/clients");
            return result ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching client history");
            return new();
        }
    }

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
        var baseUrl = _config["Provisioning:StockShopBaseUrl"];
        var apiKey  = _config["Provisioning:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
            return false;

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

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
                $"{baseUrl.TrimEnd('/')}/api/provisioning/payments/{encodedEmail}", payload);

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

    /// <summary>
    /// Sends a PATCH to StockShopOnline to update an existing provisioning request.
    /// Only non-null fields are sent — StockShopOnline ignores nulls.
    /// </summary>
    public async Task<bool> UpdateAsync(
        string   email,
        string?  planSlug          = null,
        string?  billingCycle      = null,
        string?  storeName         = null,
        string?  storePhone        = null,
        string?  storeTimezone     = null,
        string?  businessType      = null,
        string?  currency          = null,
        decimal? federalTaxRate    = null,
        decimal? provincialTaxRate = null,
        bool?    taxInclusive      = null,
        DateTime? expiresAt        = null,
        string?  appUrl            = null,
        string?  notes             = null,
        string?  status            = null)
    {
        var baseUrl = _config["Provisioning:StockShopBaseUrl"];
        var apiKey  = _config["Provisioning:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Provisioning:StockShopBaseUrl or Provisioning:ApiKey not configured.");
            return false;
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

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
                $"{baseUrl.TrimEnd('/')}/api/provisioning/request/{encodedEmail}", payload);

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
        var baseUrl = _config["Provisioning:StockShopBaseUrl"];
        var apiKey  = _config["Provisioning:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Provisioning:StockShopBaseUrl or Provisioning:ApiKey not configured.");
            return false;
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        var payload = new
        {
            email,
            firstName,
            lastName,
            planSlug,
            billingCycle,
            storeName,
            storePhone,
            storeTimezone,
            businessType,
            currency,
            federalTaxRate,
            provincialTaxRate,
            taxInclusive,
            marketingUserId
        };

        try
        {
            var response = await client.PostAsJsonAsync(
                $"{baseUrl.TrimEnd('/')}/api/provisioning/request", payload);

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
