namespace Buriza.Core.Models.Transaction;

public record Utxo
{
    public required string TxHash { get; init; }
    public required int OutputIndex { get; init; }
    public required ulong Value { get; init; }
    public string? Address { get; init; }
    public List<Asset> Assets { get; init; } = [];
    public byte[] Raw { get; init; } = [];
}
