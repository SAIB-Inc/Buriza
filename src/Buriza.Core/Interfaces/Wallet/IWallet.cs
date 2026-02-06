using Buriza.Core.Models.Transaction;
using Chrysalis.Wallet.Models.Keys;

namespace Buriza.Core.Interfaces.Wallet;

/// <summary>
/// Wallet operations interface - query and transaction capabilities.
/// </summary>
public interface IWallet
{
    /// <summary>Gets the total balance (in lovelace) for the active account.</summary>
    Task<ulong> GetBalanceAsync(int? accountIndex = null, CancellationToken ct = default);

    /// <summary>Gets UTxOs for the active account.</summary>
    Task<IReadOnlyList<Utxo>> GetUtxosAsync(int? accountIndex = null, CancellationToken ct = default);

    /// <summary>Gets native assets for the active account.</summary>
    Task<IReadOnlyList<Asset>> GetAssetsAsync(int? accountIndex = null, CancellationToken ct = default);

    /// <summary>Gets transaction history for the active account.</summary>
    Task<IReadOnlyList<TransactionHistory>> GetTransactionHistoryAsync(int? accountIndex = null, int limit = 50, CancellationToken ct = default);

    /// <summary>Builds an unsigned transaction for a simple send.</summary>
    Task<UnsignedTransaction> BuildTransactionAsync(ulong amount, string toAddress, CancellationToken ct = default);

    /// <summary>Builds an unsigned transaction from a request.</summary>
    Task<UnsignedTransaction> BuildTransactionAsync(TransactionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Signs an unsigned transaction with a private key.
    /// For Cardano, uses Chrysalis.Tx signing.
    /// </summary>
    object Sign(UnsignedTransaction unsignedTx, PrivateKey privateKey);

    /// <summary>
    /// Submits a signed transaction.
    /// For Cardano, expects Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction.
    /// </summary>
    Task<string> SubmitAsync(object signedTx, CancellationToken ct = default);
}
