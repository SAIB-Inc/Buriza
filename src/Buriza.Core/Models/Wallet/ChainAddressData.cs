using Buriza.Core.Models.Chain;

namespace Buriza.Core.Models.Wallet;

/// <summary>
/// Chain-specific address data for an account.
/// Stores only the primary receive address. Change addresses derived on-demand during tx build.
/// </summary>
public class ChainAddressData
{
    public required ChainInfo ChainInfo { get; init; }

    /// <summary>Primary address (base address for Cardano: payment + staking).</summary>
    public required string Address { get; set; }

    /// <summary>Last time this chain data was synced with the network.</summary>
    public DateTime? LastSyncedAt { get; set; }
}
