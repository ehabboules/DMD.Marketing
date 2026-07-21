using DMD.Marketing.Config;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DMD.Marketing.Services;

// ── Result & Exception ──────────────────────────────────────────────────────

public record RailwayDbResult(
    string ServiceId,
    string ConnectionString,
    string Host,
    string Port,
    string DbName,
    string Username,
    string Password
);

public class RailwayProvisioningException : Exception
{
    public RailwayProvisioningException(string message) : base(message) { }
    public RailwayProvisioningException(string message, Exception inner) : base(message, inner) { }
}

// ── Interface ───────────────────────────────────────────────────────────────

public interface IRailwayService
{
    Task<RailwayDbResult> CreateDatabaseAsync(string slug, CancellationToken ct);
    Task DeleteServiceAsync(string serviceId, CancellationToken ct);
}

// ── Service ─────────────────────────────────────────────────────────────────

public partial class RailwayService : IRailwayService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RailwayApiConfig   _config;
    private readonly ILogger<RailwayService> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
    };

    public RailwayService(
        IHttpClientFactory httpClientFactory,
        IOptions<RailwayApiConfig> config,
        ILogger<RailwayService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config            = config.Value;
        _logger            = logger;
    }

    // ── Public API ──────────────────────────────────────────────────────────

    public async Task<RailwayDbResult> CreateDatabaseAsync(string slug, CancellationToken ct)
    {
        EnsureConfigured();

        var serviceName = SanitizeSlug(slug);
        if (string.IsNullOrEmpty(serviceName))
            throw new RailwayProvisioningException($"Slug '{slug}' produced an empty service name after sanitization.");

        // 1. Create the Postgres service
        _logger.LogInformation("Creating Railway Postgres service '{Name}'…", serviceName);
        var serviceId = await CreateServiceAsync(serviceName, ct);

        // 2. Trigger deployment
        _logger.LogInformation("Triggering deployment for service {ServiceId}…", serviceId);
        await DeployAsync(serviceId, ct);

        // 3. Poll until SUCCESS (3-minute timeout, 5-second interval)
        _logger.LogInformation("Waiting for service {ServiceId} to become ready…", serviceId);
        await WaitForDeploymentAsync(serviceId, ct);

        // 4. Fetch variables and build result
        _logger.LogInformation("Fetching connection variables for service {ServiceId}…", serviceId);
        var vars = await GetVariablesAsync(serviceId, ct);

        var host     = vars.GetValueOrDefault("PGHOST", "");
        var port     = vars.GetValueOrDefault("PGPORT", "5432");
        var dbName   = vars.GetValueOrDefault("PGDATABASE", "railway");
        var username = vars.GetValueOrDefault("PGUSER", "postgres");
        var password = vars.GetValueOrDefault("PGPASSWORD", "");

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(password))
            throw new RailwayProvisioningException(
                $"Service {serviceId} is ready but connection variables are incomplete — " +
                $"PGHOST='{host}', PGPASSWORD={(string.IsNullOrEmpty(password) ? "empty" : "set")}.");

        var connectionString =
            $"Host={host};Port={port};Database={dbName};Username={username};Password={password};" +
            "SSL Mode=Require;Trust Server Certificate=true";

        _logger.LogInformation("Railway database provisioned: {ServiceId} at {Host}:{Port}/{Db}",
            serviceId, host, port, dbName);

        return new RailwayDbResult(serviceId, connectionString, host, port, dbName, username, password);
    }

    public async Task DeleteServiceAsync(string serviceId, CancellationToken ct)
    {
        EnsureConfigured();

        const string mutation = """
            mutation serviceDelete($id: String!) {
              serviceDelete(id: $id)
            }
            """;

        _logger.LogInformation("Deleting Railway service {ServiceId}…", serviceId);
        await GraphQlAsync(mutation, new { id = serviceId }, ct);
        _logger.LogInformation("Railway service {ServiceId} deleted", serviceId);
    }

    // ── Step implementations ────────────────────────────────────────────────

    private async Task<string> CreateServiceAsync(string name, CancellationToken ct)
    {
        const string mutation = """
            mutation serviceCreate($input: ServiceCreateInput!) {
              serviceCreate(input: $input) { id name }
            }
            """;

        var variables = new
        {
            input = new
            {
                projectId = _config.ProjectId,
                name,
                source = new { image = "ghcr.io/railwayapp-templates/postgres-ssl:edge" }
            }
        };

        var data = await GraphQlAsync(mutation, variables, ct);

        if (!data.TryGetProperty("serviceCreate", out var svc) ||
            !svc.TryGetProperty("id", out var idProp))
            throw new RailwayProvisioningException(
                $"serviceCreate returned unexpected shape for '{name}': {data.GetRawText()}");

        var id = idProp.GetString();
        if (string.IsNullOrEmpty(id))
            throw new RailwayProvisioningException($"serviceCreate returned empty id for '{name}'.");

        _logger.LogInformation("Railway service created: {ServiceId} ({Name})", id, name);
        return id;
    }

    private async Task DeployAsync(string serviceId, CancellationToken ct)
    {
        const string mutation = """
            mutation serviceInstanceDeployV2($serviceId: String!, $environmentId: String!) {
              serviceInstanceDeployV2(serviceId: $serviceId, environmentId: $environmentId)
            }
            """;

        await GraphQlAsync(mutation, new { serviceId, environmentId = _config.EnvironmentId }, ct);
        _logger.LogInformation("Deployment triggered for service {ServiceId}", serviceId);
    }

    private async Task WaitForDeploymentAsync(string serviceId, CancellationToken ct)
    {
        const string query = """
            query deployments($input: DeploymentListInput!, $first: Int) {
              deployments(input: $input, first: $first) {
                edges { node { id status } }
              }
            }
            """;

        var variables = new
        {
            input = new
            {
                projectId     = _config.ProjectId,
                serviceId,
                environmentId = _config.EnvironmentId
            },
            first = 1
        };

        var deadline = DateTime.UtcNow.AddMinutes(3);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(5), ct);

            var data = await GraphQlAsync(query, variables, ct);

            if (data.TryGetProperty("deployments", out var deployments) &&
                deployments.TryGetProperty("edges", out var edges) &&
                edges.GetArrayLength() > 0)
            {
                var status = edges[0].GetProperty("node").GetProperty("status").GetString();
                _logger.LogDebug("Service {ServiceId} status: {Status}", serviceId, status);

                if (status == "SUCCESS") return;
                if (status is "FAILED" or "CRASHED")
                    throw new RailwayProvisioningException(
                        $"Deployment for service {serviceId} ended with status '{status}'.");
            }
        }

        throw new RailwayProvisioningException(
            $"Timed out after 3 minutes waiting for service {serviceId} to become ready.");
    }

    private async Task<Dictionary<string, string>> GetVariablesAsync(
        string serviceId, CancellationToken ct)
    {
        const string query = """
            query variables($projectId: String!, $environmentId: String!, $serviceId: String) {
              variables(projectId: $projectId, environmentId: $environmentId, serviceId: $serviceId)
            }
            """;

        var variables = new
        {
            projectId     = _config.ProjectId,
            environmentId = _config.EnvironmentId,
            serviceId
        };

        var data = await GraphQlAsync(query, variables, ct);

        if (!data.TryGetProperty("variables", out var varsElement))
            throw new RailwayProvisioningException(
                $"variables query returned no data for service {serviceId}.");

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(
            varsElement.GetRawText(), _json);

        if (dict is null || dict.Count == 0)
            throw new RailwayProvisioningException(
                $"variables query returned empty result for service {serviceId}.");

        _logger.LogInformation("Retrieved {Count} variables for service {ServiceId}", dict.Count, serviceId);
        return dict;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_config.ApiToken) ||
            string.IsNullOrWhiteSpace(_config.ProjectId) ||
            string.IsNullOrWhiteSpace(_config.EnvironmentId))
            throw new RailwayProvisioningException(
                "Railway config incomplete — ApiToken, ProjectId, or EnvironmentId missing.");
    }

    private static string SanitizeSlug(string slug) =>
        SlugRegex().Replace(slug.Trim().ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex SlugRegex();

    private async Task<JsonElement> GraphQlAsync(
        string query, object? variables, CancellationToken ct)
    {
        var http    = _httpClientFactory.CreateClient("Railway");
        var payload = JsonSerializer.Serialize(new { query, variables }, _json);
        var body    = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync("", body, ct);
        }
        catch (Exception ex)
        {
            throw new RailwayProvisioningException("Failed to reach Railway API.", ex);
        }

        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new RailwayProvisioningException(
                $"Railway API returned HTTP {(int)response.StatusCode}: {json}");

        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("errors", out var errors))
            throw new RailwayProvisioningException(
                $"Railway GraphQL error: {errors.GetRawText()}");

        if (!doc.RootElement.TryGetProperty("data", out var data))
            throw new RailwayProvisioningException(
                $"Railway response missing 'data' field: {json}");

        return data.Clone();
    }
}
