namespace Buriza.Data.Models.Common;

public class WalletAccount
{
    public string Name { get; set; } = "";
    public string DerivationPath { get; set; } = "";
    public string Avatar { get; set; } = "";
    public bool IsActive { get; set; } = false;
}

public class Wallet
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Avatar { get; set; } = "";
    public List<WalletAccount> Accounts { get; set; } = [];
    public bool IsExpanded { get; set; } = false;
}