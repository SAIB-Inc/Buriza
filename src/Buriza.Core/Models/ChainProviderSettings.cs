namespace Buriza.Core.Models;

/// <summary>
/// Configuration settings for chain providers, typically loaded from appsettings.json.
/// </summary>
public class ChainProviderSettings
{
    public CardanoSettings? Cardano { get; set; }
}

public class CardanoSettings
{
    // API keys (required for Demeter, optional for self-hosted)
    public string? MainnetApiKey { get; set; }
    public string? PreprodApiKey { get; set; }
    public string? PreviewApiKey { get; set; }

    // Custom endpoints - if set, overrides Demeter default
    public string? MainnetEndpoint { get; set; }
    public string? PreprodEndpoint { get; set; }
    public string? PreviewEndpoint { get; set; }
}
