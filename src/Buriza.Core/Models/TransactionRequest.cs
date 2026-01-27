using Buriza.Data.Models.Common;

namespace Buriza.Core.Models;

public record TransactionRequest
{
    public required string FromAddress { get; init; }
    public required List<TransactionRecipient> Recipients { get; init; }
    public string? Message { get; init; }
}

public record TransactionRecipient
{
    public required string Address { get; init; }
    public required ulong Amount { get; init; }
    public List<ChainAsset>? Assets { get; init; }
}
