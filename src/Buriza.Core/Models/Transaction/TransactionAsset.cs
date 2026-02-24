namespace Buriza.Core.Models.Transaction;

public record TransactionAsset(
    string AssetIcon,
    string AssetName,
    decimal Amount,
    decimal UsdValue
);
