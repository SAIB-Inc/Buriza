using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Transaction;

namespace Buriza.Core.Interfaces.Chain;

/// <summary>
/// Buriza chain provider interface for query operations.
/// Pure protocol operations - no chain-specific derivation or signing.
/// </summary>
public interface IBurizaChainProvider : IDisposable
{
    /// <summary>Validates the connection to the endpoint.</summary>
    Task<bool> ValidateConnectionAsync(CancellationToken ct = default);

    /// <summary>Gets UTxOs for the given address.</summary>
    Task<IReadOnlyList<Utxo>> GetUtxosAsync(string address, CancellationToken ct = default);

    /// <summary>Gets the total balance (in base units) for the given address.</summary>
    Task<ulong> GetBalanceAsync(string address, CancellationToken ct = default);

    /// <summary>Gets native assets for the given address.</summary>
    Task<IReadOnlyList<Asset>> GetAssetsAsync(string address, CancellationToken ct = default);

    /// <summary>Checks if an address has been used (has UTxOs).</summary>
    Task<bool> IsAddressUsedAsync(string address, CancellationToken ct = default);

    /// <summary>Reads a transaction by hash. Returns chain-specific transaction object.</summary>
    Task<byte[]?> ReadTxAsync(string hash, CancellationToken ct = default);

    /// <summary>Submits a raw transaction.</summary>
    Task<string> SubmitAsync(byte[] txBytes, CancellationToken ct = default);

    /// <summary>Follows the chain tip for real-time updates.</summary>
    IAsyncEnumerable<TipEvent> FollowTipAsync(CancellationToken ct = default);
}
