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

    /// <summary>Primary receive address (index 0, role 0).</summary>
    public required string ReceiveAddress { get; set; }

    /// <summary>Staking/reward address (role 2, index 0). Null if not yet derived.</summary>
    public string? StakingAddress { get; set; }

    /// <summary>Last time this chain data was synced with the network.</summary>
    public DateTime? LastSyncedAt { get; set; }
}
