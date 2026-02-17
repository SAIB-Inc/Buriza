using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Wallet;

/// <summary>
/// Chain-specific address data for an account.
/// Stores only the primary receive address. Change addresses derived on-demand during tx build.
/// </summary>
public class ChainAddressData
{
    public required ChainType Chain { get; init; }
    public required NetworkType Network { get; init; }

    /// <summary>Native currency symbol (e.g. "ADA", "tADA", "BTC").</summary>
    public required string Symbol { get; init; }

    /// <summary>Primary address (base address for Cardano: payment + staking).</summary>
    public required string Address { get; set; }

    /// <summary>Staking/reward address (role 2, index 0). Null for chains without staking.</summary>
    public string? StakingAddress { get; set; }

    /// <summary>Last time this chain data was synced with the network.</summary>
    public DateTime? LastSyncedAt { get; set; }
}
