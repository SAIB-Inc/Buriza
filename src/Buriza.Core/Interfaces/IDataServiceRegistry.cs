using Buriza.Core.Models;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Interfaces;

/// <summary>
/// Registry for data service configurations.
/// Allows users to configure custom endpoints for services like token metadata, prices, etc.
/// </summary>
public interface IDataServiceRegistry
{
    /// <summary>Gets custom config for a data service. Returns null if using default.</summary>
    Task<DataServiceConfig?> GetConfigAsync(DataServiceType serviceType, CancellationToken ct = default);

    /// <summary>
    /// Sets custom config for a data service. API key is encrypted with password.
    /// Pass null for apiKey to use default from app settings.
    /// </summary>
    Task SetConfigAsync(DataServiceType serviceType, string? endpoint, string? apiKey, string password, string? name = null, CancellationToken ct = default);

    /// <summary>Removes custom config, reverting to app settings default.</summary>
    Task ClearConfigAsync(DataServiceType serviceType, CancellationToken ct = default);

    /// <summary>
    /// Loads custom config into session (endpoint and decrypted API key).
    /// Call this after wallet unlock to enable custom service for the session.
    /// </summary>
    Task LoadConfigAsync(DataServiceType serviceType, string password, CancellationToken ct = default);

    /// <summary>Loads all custom configs into session.</summary>
    Task LoadAllConfigsAsync(string password, CancellationToken ct = default);

    /// <summary>Gets all configured (non-default) services.</summary>
    Task<IReadOnlyList<DataServiceConfig>> GetAllConfigsAsync(CancellationToken ct = default);
}
