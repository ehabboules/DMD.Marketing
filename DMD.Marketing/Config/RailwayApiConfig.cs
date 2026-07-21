namespace DMD.Marketing.Config;

/// <summary>
/// Configuration for Railway GraphQL API provisioning.
/// Binds to the "Railway" section in appsettings.json.
/// </summary>
public class RailwayApiConfig
{
    /// <summary>Railway API token for GraphQL authentication.</summary>
    public string? ApiToken { get; set; }

    /// <summary>Railway project ID (e.g., 12345abc-def0-1234-5678-90abcdef1234).</summary>
    public string? ProjectId { get; set; }

    /// <summary>Railway environment ID (e.g., production, staging).</summary>
    public string? EnvironmentId { get; set; }
}
