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

public record TransactionSummary
{
    public required TransactionActionType Type { get; init; }
    public required List<TransactionOutput> Outputs { get; init; }
    public ulong TotalAmount { get; init; }
}

public record TransactionOutput
{
    public required string Address { get; init; }
    public required ulong Amount { get; init; }
    public List<Asset>? Assets { get; init; }
}
