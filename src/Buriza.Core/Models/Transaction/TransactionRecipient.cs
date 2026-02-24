namespace Buriza.Core.Models.Transaction;

public record TransactionRecipient(
    string Address,
    ulong Amount,
    List<Asset>? Assets = null);