namespace Buriza.Core.Models.Transaction;

public record UnsignedTransaction(
    byte[] TxRaw,
    ulong Fee,
    TransactionSummary? Summary = null);
