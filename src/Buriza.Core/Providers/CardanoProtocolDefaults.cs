namespace Buriza.Core.Providers;

/// <summary>
/// Hardcoded protocol parameter defaults for Cardano networks.
/// Used as fallback when UTxO RPC returns null for BigInt fields (proto mismatch issue).
/// Values from preprod as of Jan 2025.
/// </summary>
public static class CardanoProtocolDefaults
{
    // Fee parameters
    public const ulong MinFeeCoefficient = 44;
    public const ulong MinFeeConstant = 155381;
    public const ulong CoinsPerUtxoByte = 4310;

    // Size limits
    public const uint MaxTxSize = 16384;
    public const uint MaxBlockBodySize = 90112;
    public const uint MaxBlockHeaderSize = 1100;

    // Deposits
    public const ulong StakeKeyDeposit = 2_000_000;
    public const ulong PoolDeposit = 500_000_000;

    // Pool parameters
    public const uint DesiredNumberOfPools = 500;
    public const ulong MinPoolCost = 170_000_000;

    // Protocol version
    public const int ProtocolVersionMajor = 9;
    public const int ProtocolVersionMinor = 0;

    // Value limits
    public const uint MaxValueSize = 5000;
    public const uint CollateralPercentage = 150;
    public const uint MaxCollateralInputs = 3;

    // Execution units
    public const ulong MaxTxExUnitsMemory = 14_000_000;
    public const ulong MaxTxExUnitsSteps = 10_000_000_000;
    public const ulong MaxBlockExUnitsMemory = 62_000_000;
    public const ulong MaxBlockExUnitsSteps = 20_000_000_000;

    // Execution prices (as rationals)
    public const ulong ExPricesMemoryNumerator = 577;
    public const ulong ExPricesMemoryDenominator = 10_000;
    public const ulong ExPricesStepsNumerator = 721;
    public const ulong ExPricesStepsDenominator = 10_000_000;

    // Governance parameters
    public const uint MinCommitteeSize = 7;
    public const ulong CommitteeTermLimit = 146;
    public const ulong GovernanceActionValidityPeriod = 6;
    public const ulong GovernanceActionDeposit = 100_000_000_000;
    public const ulong DRepDeposit = 500_000_000;
    public const ulong DRepInactivityPeriod = 20;

    // Script ref cost
    public const ulong MinFeeRefScriptCostPerByteNumerator = 15;
    public const ulong MinFeeRefScriptCostPerByteDenominator = 1;
}
