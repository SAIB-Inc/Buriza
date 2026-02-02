using Buriza.Data.Models.Enums;

namespace Buriza.Data.Services;

/// <summary>
/// Provides session-based custom endpoint and API key overrides for data services.
/// Implemented by ISessionService in Buriza.Core.
/// </summary>
public interface IDataServiceSessionProvider
{
    /// <summary>Gets a custom endpoint for a data service. Returns null to use default.</summary>
    string? GetDataServiceEndpoint(DataServiceType serviceType);

    /// <summary>Gets a custom API key for a data service. Returns null to use default.</summary>
    string? GetDataServiceApiKey(DataServiceType serviceType);
}
