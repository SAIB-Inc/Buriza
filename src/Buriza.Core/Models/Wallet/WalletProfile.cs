namespace Buriza.Core.Models.Wallet;

/// <summary>
/// Global wallet profile for customization.
/// </summary>
public class WalletProfile
{
    public required string Name { get; set; }
    public string? Label { get; set; }
    public string? Avatar { get; set; }
}
