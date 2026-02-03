using Buriza.Core.Models;
using Buriza.Data.Models.Common;
using Asset = Buriza.Data.Models.Common.Asset;

namespace Buriza.Core.Interfaces.Wallet;

/// <summary>
/// Wallet operations interface - query and transaction capabilities.
/// </summary>
public interface IWallet
{
    // Query operations
    Task<ulong> GetBalanceAsync(int? accountIndex = null, CancellationToken ct = default);
    Task<IReadOnlyList<Utxo>> GetUtxosAsync(int? accountIndex = null, CancellationToken ct = default);
    Task<IReadOnlyList<Asset>> GetAssetsAsync(int? accountIndex = null, CancellationToken ct = default);
    Task<IReadOnlyList<TransactionHistory>> GetTransactionHistoryAsync(int? accountIndex = null, int limit = 50, CancellationToken ct = default);

    // Transaction operations
    Task<UnsignedTransaction> BuildTransactionAsync(ulong amount, string toAddress, CancellationToken ct = default);
    Task<UnsignedTransaction> BuildTransactionAsync(TransactionRequest request, CancellationToken ct = default);
    Task<string> SubmitAsync(Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction tx, CancellationToken ct = default);
}
