namespace Buriza.Core.Models.Wallet;

/// <summary>
/// Account within an HD wallet (BIP-44 account level).
/// m / purpose' / coin_type' / account' / ...
/// </summary>
public class BurizaWalletAccount
{
    /// <summary>BIP-44 account index (hardened).</summary>
    public required int Index { get; init; }

    /// <summary>Account name (e.g., "Savings", "Trading").</summary>
    public required string Name { get; set; }

    /// <summary>Optional avatar/image for visual distinction between accounts.</summary>
    public string? Avatar { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
