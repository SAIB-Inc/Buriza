using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Transaction;

public record TransactionSummary(
    TransactionActionType Type,
    List<TransactionOutput> Outputs,
    ulong TotalAmount);
