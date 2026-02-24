namespace Buriza.Core.Models.Transaction;

public record TransactionOutput(
    string Address,
    ulong Amount,
    List<Asset>? Assets = null);