using Buriza.Core.Models.Enums;

namespace Buriza.Core.Interfaces.DataServices;

/// <summary>
/// Provides session-based custom endpoint and API key overrides for data services.
/// Implemented by AppStateService or similar UI state service.
/// </summary>
public interface IDataServiceSessionProvider
{
    /// <summary>Gets a custom endpoint for a data service. Returns null to use default.</summary>
    string? GetDataServiceEndpoint(DataServiceType serviceType);

    /// <summary>Gets a custom API key for a data service. Returns null to use default.</summary>
    string? GetDataServiceApiKey(DataServiceType serviceType);
}
