namespace Buriza.Core.Models.Transaction;

public record UnsignedTransaction
{
    public required byte[] TxRaw { get; init; }
    public required ulong Fee { get; init; }
    public TransactionSummary? Summary { get; init; }
}
