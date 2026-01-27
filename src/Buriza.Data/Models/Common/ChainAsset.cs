namespace Buriza.Data.Models.Common;

public record ChainAsset
{
    public required string PolicyId { get; init; }
    public required string AssetName { get; init; }
    public required string HexName { get; init; }
    public required ulong Quantity { get; init; }

    public string Subject => $"{PolicyId}{HexName}";
}
