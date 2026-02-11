using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Transaction;

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
