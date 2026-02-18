using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Chain;

public record TipEvent(
    TipAction Action,
    ulong Slot,
    string Hash
);
