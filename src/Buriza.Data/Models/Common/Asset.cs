namespace Buriza.Data.Models.Common;

public record Asset
{
    public required string PolicyId { get; init; }
    public required string AssetName { get; init; }
    public required string HexName { get; init; }
    public required ulong Quantity { get; init; }

    public string Subject => $"{PolicyId}{HexName}";
}

public record TokenAsset(
    string AssetIcon,
    string AssetName,
    float PricePercentage,
    float AssetAmount,
    float UsdConversion
);

public record NftAsset(
    string AssetIcon,
    string AssetName,
    string CollectionName,
    float AssetAmount,
    float PricePercentage,
    float UsdConversion
);