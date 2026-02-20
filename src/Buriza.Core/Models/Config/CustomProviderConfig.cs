using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Config;

/// <summary>
/// User-customizable provider configuration.
/// Endpoint stored in plain storage, API key stored encrypted.
/// </summary>
public record CustomProviderConfig
{
    public required ChainType Chain { get; init; }
    public required string Network { get; init; }

    /// <summary>Custom endpoint URL (not sensitive, stored plain).</summary>
    public string? Endpoint { get; init; }

    /// <summary>Whether a custom API key is stored (the key itself is encrypted separately).</summary>
    public bool HasCustomApiKey { get; init; }

    /// <summary>User-friendly name for this configuration.</summary>
    public string? Name { get; init; }
}
