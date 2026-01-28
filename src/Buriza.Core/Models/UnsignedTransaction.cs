using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;

namespace Buriza.Core.Models;

public record UnsignedTransaction
{
    public required ChainType ChainType { get; init; }
    public required Transaction Transaction { get; init; }
    public required ulong Fee { get; init; }
    public TransactionSummary? Summary { get; init; }
}

public record TransactionSummary
{
    public required string Type { get; init; }
    public required List<TransactionOutput> Outputs { get; init; }
    public ulong TotalAmount { get; init; }
}

public record TransactionOutput
{
    public required string Address { get; init; }
    public required ulong Amount { get; init; }
    public List<Asset>? Assets { get; init; }
}
