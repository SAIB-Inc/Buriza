using Buriza.Core.Models.Transaction;

namespace Buriza.Core.Interfaces.Wallet;

/// <summary>
/// Wallet operations interface - query capabilities.
/// </summary>
public interface IWallet
{
    /// <summary>Gets the total balance (in base units) for the active account.</summary>
    Task<ulong> GetBalanceAsync(int? accountIndex = null, CancellationToken ct = default);

    /// <summary>Gets UTxOs for the active account.</summary>
    Task<IReadOnlyList<Utxo>> GetUtxosAsync(int? accountIndex = null, CancellationToken ct = default);

    /// <summary>Gets native assets for the active account.</summary>
    Task<IReadOnlyList<Asset>> GetAssetsAsync(int? accountIndex = null, CancellationToken ct = default);
}
