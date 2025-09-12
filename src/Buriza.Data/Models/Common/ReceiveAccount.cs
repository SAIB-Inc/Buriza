using Buriza.Data.Models.Enums;

namespace Buriza.Data.Models.Common;

public record ReceiveAccount(
    int Index,
    string AvatarUrl,
    string Name,
    string Address,
    string DerivationPath,
    AccountStatusType Status,
    decimal Balance = 0m
)
{
    public string Start { get; init; } = SplitAddressPart(Address, 0);
    public string Middle { get; init; } = SplitAddressPart(Address, 1);
    public string End { get; init; } = SplitAddressPart(Address, 2);

    private static string SplitAddressPart(string address, int part, int numChars = 6)
    {
        if (string.IsNullOrEmpty(address))
            return "";

        if (address.Length <= numChars * 2)
            return part == 0 ? address : "";

        return part switch
        {
            0 => address[..numChars],      // Start
            1 => address[numChars..^numChars], // Middle
            2 => address[^numChars..],     // End
            _ => ""
        };
    }
}