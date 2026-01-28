using Buriza.Core.Models;
using Chrysalis.Wallet.Models.Keys;

namespace Buriza.Core.Interfaces.Chain;

public interface ITransactionService
{
    Task<UnsignedTransaction> BuildAsync(TransactionRequest request, CancellationToken ct = default);
    Task<string> SignAsync(UnsignedTransaction tx, PrivateKey privateKey, CancellationToken ct = default);
    Task<string> SubmitAsync(string signedTx, CancellationToken ct = default);
}
