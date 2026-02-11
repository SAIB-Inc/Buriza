using Buriza.Core.Models.DataServices;

namespace Buriza.Core.Interfaces.DataServices;

public interface ITokenPriceService
{
    Task<Dictionary<string, decimal>?> GetTokenPricesAsync(List<string> units, CancellationToken ct = default);
    Task<decimal?> GetTokenPriceAsync(string unit, CancellationToken ct = default);
    Task<TokenPriceChange?> GetPriceChangeAsync(string unit, string? timeframes = null, CancellationToken ct = default);
    Task<decimal?> GetAdaPriceAsync(string quote = "USD", CancellationToken ct = default);
}
