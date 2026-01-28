using Buriza.Core.Models;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Wallet.Models.Keys;

namespace Buriza.Core.Interfaces.Chain;

public interface ITransactionService
{
    Task<UnsignedTransaction> BuildAsync(TransactionRequest request, CancellationToken ct = default);
    Task<Transaction> SignAsync(UnsignedTransaction tx, PrivateKey privateKey, CancellationToken ct = default);
    Task<string> SubmitAsync(Transaction tx, CancellationToken ct = default);
    Task<string> TransferAsync(TransactionRequest request, PrivateKey privateKey, CancellationToken ct = default);
}
