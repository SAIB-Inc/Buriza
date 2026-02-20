namespace Buriza.Data.Models.Common;

public class WalletAccount
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string DerivationPath { get; set; } = "";
    public string Avatar { get; set; } = "";
    public bool IsActive { get; set; } = false;

    // TODO: Update once Cardano address validation is in place
    public string Start => Address.Length > 12 ? Address[..6] : Address;
    public string Middle => Address.Length > 12 ? Address[6..^6] : "";
    public string End => Address.Length > 12 ? Address[^6..] : "";
}

public class Wallet
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Avatar { get; set; } = "";
    public List<WalletAccount> Accounts { get; set; } = [];
    public bool IsExpanded { get; set; } = false;
}