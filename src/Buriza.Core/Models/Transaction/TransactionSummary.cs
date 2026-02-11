using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Transaction;

public record TransactionSummary
{
    public required TransactionActionType Type { get; init; }
    public required List<TransactionOutput> Outputs { get; init; }
    public ulong TotalAmount { get; init; }
}
