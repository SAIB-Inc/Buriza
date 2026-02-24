using Buriza.Core.Models.Wallet;

namespace Buriza.Core.Chain.Cardano;

/// <summary>
/// Cardano-specific address data extending the base chain address data.
/// Includes the staking/reward address derived from the staking key.
/// </summary>
public class CardanoAddressData : ChainAddressData
{
    /// <summary>Staking/reward address (bech32, e.g. stake1... or stake_test1...).</summary>
    public required string StakingAddress { get; init; }
}
