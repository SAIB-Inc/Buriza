namespace Buriza.Core.Models.Transaction;

public record TransactionRequest
{
    public required List<TransactionRecipient> Recipients { get; init; }
    public string? Message { get; init; }
    public Dictionary<ulong, object>? Metadata { get; init; }
}
