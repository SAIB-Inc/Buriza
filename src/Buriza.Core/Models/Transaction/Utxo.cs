namespace Buriza.Core.Models.Transaction;

public record Utxo(
    string TxHash,
    int OutputIndex,
    ulong Value,
    string Address,
    List<Asset> Assets,
    byte[] Raw
);
