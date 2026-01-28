using Buriza.Data.Models.Enums;

namespace Buriza.Data.Models.Common;

public class WalletAccount
{
    public int Index { get; set; }
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
    public ChainType ActiveChain { get; set; } = ChainType.Cardano;
    public List<WalletAccount> Accounts { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsExpanded { get; set; } = false;
}