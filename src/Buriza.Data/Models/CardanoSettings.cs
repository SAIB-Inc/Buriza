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
}
