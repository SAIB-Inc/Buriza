namespace Buriza.Core.Models.Enums;

/// <summary>
/// Transaction action type for building/signing transactions.
/// </summary>
public enum TransactionActionType
{
    Send,
    Delegate,
    Withdraw,
    RegisterStake,
    DeregisterStake,
    Mint,
    Burn,
    Vote,
    Other
}
