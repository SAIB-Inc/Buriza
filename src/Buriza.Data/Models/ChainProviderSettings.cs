namespace Buriza.Data.Models;

/// <summary>
/// Configuration settings for chain providers, typically loaded from appsettings.json.
/// </summary>
public class ChainProviderSettings
{
    public CardanoSettings? Cardano { get; set; }
}
