using System.Net.Http.Json;
using DMD.Marketing.Models;

namespace DMD.Marketing.Services;

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

    public async Task<bool> RequestActivationAsync(
        string email,
        string firstName,
        string lastName,
        string planSlug,
        string billingCycle,
        string storeName,
        string? storePhone,
        string? storeTimezone,
        string? businessType,
        int? marketingUserId)
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
            marketingUserId
        };

        try
        {
            var response = await client.PostAsJsonAsync(
                $"{baseUrl.TrimEnd('/')}/api/provisioning/request", payload);

            if (response.IsSuccessStatusCode) return true;

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Provisioning request failed: {Status} — {Body}",
                response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling provisioning API");
            return false;
        }
    }
}
