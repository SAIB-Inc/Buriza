namespace Buriza.Core.Providers.Cardano;

/// <summary>
/// Hardcoded protocol parameter defaults for Cardano networks.
/// Used as fallback when UTxO RPC endpoint returns incomplete data.
/// TODO: Remove this file once the UTxO RPC endpoint returns complete parameters.
/// </summary>
public static class ProtocolParamsDefaults
{
    public const ulong MinFeeCoefficient = 44;
    public const ulong MinFeeConstant = 155381;
    public const ulong CoinsPerUtxoByte = 4310;
    public const uint MaxTxSize = 16384;
    public const uint MaxBlockBodySize = 90112;
    public const uint MaxBlockHeaderSize = 1100;
    public const ulong StakeKeyDeposit = 2000000;
    public const ulong PoolDeposit = 500000000;
    public const uint DesiredNumberOfPools = 500;
    public const ulong MinPoolCost = 170000000;
    public const int ProtocolVersionMajor = 9;
    public const int ProtocolVersionMinor = 0;
    public const uint MaxValueSize = 5000;
    public const uint CollateralPercentage = 150;
    public const uint MaxCollateralInputs = 3;
    public const ulong MaxTxExUnitsMemory = 14000000;
    public const ulong MaxTxExUnitsSteps = 10000000000;
    public const ulong MaxBlockExUnitsMemory = 62000000;
    public const ulong MaxBlockExUnitsSteps = 20000000000;
    public const ulong ExPricesMemoryNumerator = 577;
    public const ulong ExPricesMemoryDenominator = 10000;
    public const ulong ExPricesStepsNumerator = 721;
    public const ulong ExPricesStepsDenominator = 10000000;
    public const uint MinCommitteeSize = 7;
    public const ulong CommitteeTermLimit = 146;
    public const ulong GovernanceActionValidityPeriod = 6;
    public const ulong GovernanceActionDeposit = 100000000000;
    public const ulong DRepDeposit = 500000000;
    public const ulong DRepInactivityPeriod = 20;
    public const ulong MinFeeRefScriptCostPerByteNumerator = 15;
    public const ulong MinFeeRefScriptCostPerByteDenominator = 1;
}
