using Buriza.Data.Models.Enums;

namespace Buriza.Data.Models.Common;

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