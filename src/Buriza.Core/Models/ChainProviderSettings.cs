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
    public string? MainnetApiKey { get; set; }
    public string? PreprodApiKey { get; set; }
    public string? PreviewApiKey { get; set; }
}
