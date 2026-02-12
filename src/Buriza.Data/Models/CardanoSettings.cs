namespace Buriza.Data.Models;

/// <summary>
/// Cardano provider configuration for endpoints and API keys.
/// </summary>
public class CardanoSettings
{
    public string? MainnetApiKey { get; set; }
    public string? PreprodApiKey { get; set; }
    public string? PreviewApiKey { get; set; }

    public string? MainnetEndpoint { get; set; }
    public string? PreprodEndpoint { get; set; }
    public string? PreviewEndpoint { get; set; }

    /// <summary>
    /// Optional hostname allowlist for provider endpoints.
    /// When set, remote endpoints must match one of these hosts.
    /// Loopback endpoints remain allowed for local development.
    /// </summary>
    public List<string>? AllowedEndpointHosts { get; set; }
}
