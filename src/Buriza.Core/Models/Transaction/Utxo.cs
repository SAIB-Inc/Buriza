namespace Buriza.Core.Models.Transaction;

public record Utxo(
    string TxHash,
    int OutputIndex,
    ulong Value,
    string? Address = null,
    List<Asset> Assets = null!,
    byte[] Raw = null!
);
