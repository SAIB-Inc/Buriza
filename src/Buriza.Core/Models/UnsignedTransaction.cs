using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Models;

public record UnsignedTransaction
{
    public required ChainType ChainType { get; init; }
    public required string TxHex { get; init; }
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
    public List<ChainAsset>? Assets { get; init; }
}
