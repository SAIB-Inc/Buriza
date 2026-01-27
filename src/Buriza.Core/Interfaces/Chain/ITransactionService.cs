using Buriza.Core.Models;

namespace Buriza.Core.Interfaces.Chain;

public interface ITransactionService
{
    Task<UnsignedTransaction> BuildAsync(TransactionRequest request, CancellationToken ct = default);
    Task<string> SignAsync(UnsignedTransaction tx, byte[] privateKey, CancellationToken ct = default);
    Task<string> SubmitAsync(string signedTx, CancellationToken ct = default);
}
