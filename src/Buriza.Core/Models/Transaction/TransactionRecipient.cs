namespace Buriza.Core.Models.Transaction;

public record TransactionRecipient
{
    public required string Address { get; init; }
    public required ulong Amount { get; init; }
    public List<Asset>? Assets { get; init; }
}
