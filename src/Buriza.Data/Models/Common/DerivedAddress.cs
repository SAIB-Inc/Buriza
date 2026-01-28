using Buriza.Data.Models.Enums;

namespace Buriza.Data.Models.Common;

public record DerivedAddress
{
    public required string Address { get; init; }
    public required int AccountIndex { get; init; }
    public required AddressRole Role { get; init; }
    public required int AddressIndex { get; init; }
    public required string DerivationPath { get; init; }
    public bool IsUsed { get; init; } = false;
}
