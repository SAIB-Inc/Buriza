using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Config;

/// <summary>
/// Configuration for a chain provider (e.g., UTxO RPC endpoint).
/// </summary>
public record ProviderConfig
{
    /// <summary>The chain this configuration applies to.</summary>
    public required ChainType Chain { get; init; }

    /// <summary>RPC endpoint URL.</summary>
    public required string Endpoint { get; init; }

    /// <summary>Network identifier (e.g. "mainnet", "preprod", "preview").</summary>
    public required string Network { get; init; }

    /// <summary>Optional API key for authenticated endpoints.</summary>
    public string? ApiKey { get; init; }

    /// <summary>User-friendly name for this configuration.</summary>
    public string? Name { get; init; }
}
