namespace Buriza.Core.Models.Transaction;

public record TransactionRequest(
    List<TransactionRecipient> Recipients,
    string? Message = null,
    Dictionary<ulong, object>? Metadata = null);
