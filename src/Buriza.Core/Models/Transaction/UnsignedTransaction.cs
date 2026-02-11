using Buriza.Core.Models.Enums;
using ChrysalisTransaction = Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction;

namespace Buriza.Core.Models.Transaction;

public record UnsignedTransaction
{
    public required ChainType ChainType { get; init; }
    public required ChrysalisTransaction Transaction { get; init; }
    public required ulong Fee { get; init; }
    public TransactionSummary? Summary { get; init; }
}
