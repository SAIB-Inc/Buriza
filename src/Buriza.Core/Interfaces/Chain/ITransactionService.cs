using Buriza.Core.Models;
using Chrysalis.Wallet.Models.Keys;

namespace Buriza.Core.Interfaces.Chain;

public interface ITransactionService
{
    Task<UnsignedTransaction> BuildAsync(TransactionRequest request, CancellationToken ct = default);
    Task<byte[]> SignAsync(UnsignedTransaction tx, PrivateKey privateKey, CancellationToken ct = default);
    Task<string> SubmitAsync(byte[] signedTx, CancellationToken ct = default);
}
