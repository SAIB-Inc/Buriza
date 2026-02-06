using Buriza.Core.Models.Enums;

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

/// <summary>
/// Basic wallet information for UI display (does not require chain queries).
/// </summary>
public record WalletInfo
{
    public required Guid WalletId { get; init; }
    public required string WalletName { get; init; }
    public required string AccountName { get; init; }
    public required int AccountIndex { get; init; }
    public required string ReceiveAddress { get; init; }
    public required ChainType Chain { get; init; }
    public required NetworkType Network { get; init; }
}