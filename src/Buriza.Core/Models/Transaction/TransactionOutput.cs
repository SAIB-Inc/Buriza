namespace Buriza.Core.Models.Transaction;

public record TransactionOutput
{
    public required string Address { get; init; }
    public required ulong Amount { get; init; }
    public List<Asset>? Assets { get; init; }
}
