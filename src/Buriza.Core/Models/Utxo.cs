using Buriza.Data.Models.Common;

namespace Buriza.Core.Models;

public record Utxo
{
    public required string TxHash { get; init; }
    public required int OutputIndex { get; init; }
    public required ulong Value { get; init; }
    public string? Address { get; init; }
    public List<ChainAsset> Assets { get; init; } = [];
}
