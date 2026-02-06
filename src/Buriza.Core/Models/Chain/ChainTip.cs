namespace Buriza.Core.Models.Chain;

public record ChainTip(
    ulong Slot,
    string Hash,
    ulong Timestamp
);

public enum TipAction
{
    Apply,
    Undo,
    Reset
}

public record TipEvent(
    TipAction Action,
    ulong Slot,
    string Hash
);
