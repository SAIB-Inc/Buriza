using Buriza.Core.Models.DataServices;

namespace Buriza.Core.Interfaces.DataServices;

/// <summary>
/// Token pricing service.
/// </summary>
public interface ITokenPriceService
{
    /// <summary>Gets prices for a set of units.</summary>
    Task<Dictionary<string, decimal>?> GetTokenPricesAsync(List<string> units, CancellationToken ct = default);
    /// <summary>Gets price for a single unit.</summary>
    Task<decimal?> GetTokenPriceAsync(string unit, CancellationToken ct = default);
    /// <summary>Gets price change for a unit over a timeframe.</summary>
    Task<TokenPriceChange?> GetPriceChangeAsync(string unit, CancellationToken ct = default);
    /// <summary>Gets native coin price for a quote currency.</summary>
    Task<decimal?> GetNativeCoinPriceAsync(string quote = "USD", CancellationToken ct = default);
}
