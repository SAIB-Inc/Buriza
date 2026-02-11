namespace Buriza.Core.Models.Chain;

public record ChainTip(
    ulong Slot,
    string Hash,
    ulong Timestamp
);
