namespace Buriza.Core.Models.Enums;

/// <summary>
/// Transaction action type for building/signing transactions.
/// </summary>
public enum TransactionActionType
{
    Send,
    Receive,
    Mixed,
    Other
}
