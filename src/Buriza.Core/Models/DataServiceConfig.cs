using Buriza.Data.Models.Enums;

namespace Buriza.Core.Models;

/// <summary>
/// User-customizable data service configuration.
/// Endpoint stored in plain storage, API key stored encrypted.
/// </summary>
public record DataServiceConfig
{
    public required DataServiceType ServiceType { get; init; }

    /// <summary>Custom endpoint URL (not sensitive, stored plain).</summary>
    public string? Endpoint { get; init; }

    /// <summary>Whether a custom API key is stored (the key itself is encrypted separately).</summary>
    public bool HasCustomApiKey { get; init; }

    /// <summary>User-friendly name for this configuration.</summary>
    public string? Name { get; init; }
}
