using Buriza.Data.Models.Enums;

namespace Buriza.Data.Models.Common;

public record TransactionHistory(
    string TransactionId,
    DateTime Timestamp,
    string FromAddress,
    string ToAddress,
    TransactionType Type,
    List<TransactionAsset> Assets,
    decimal CollateralAda,
    decimal TransactionFeeAda,
    string? ExpiryInfo = "No limit",
    TransactionCategory Category = TransactionCategory.Default
);

public record TransactionAsset(
    string AssetIcon,
    string AssetName,
    decimal Amount,
    decimal UsdValue
);