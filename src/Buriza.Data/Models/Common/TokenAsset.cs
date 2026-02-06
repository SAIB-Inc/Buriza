namespace Buriza.Data.Models.Common;

/// <summary>
/// UI view model for displaying fungible token assets.
/// </summary>
public record TokenAsset(
    string AssetIcon,
    string AssetName,
    float PricePercentage,
    float AssetAmount,
    float UsdConversion
);

/// <summary>
/// UI view model for displaying NFT assets.
/// </summary>
public record NftAsset(
    string AssetIcon,
    string AssetName,
    string CollectionName,
    float AssetAmount,
    float PricePercentage,
    float UsdConversion
);
