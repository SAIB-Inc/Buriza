namespace Buriza.Core.Providers;

/// <summary>
/// Cardano HD wallet derivation constants following CIP-1852.
/// </summary>
public static class CardanoDerivation
{
    public const int Purpose = 1852;
    public const int CoinType = 1815;

    public static string GetPath(int account, int role, int index)
        => $"m/{Purpose}'/{CoinType}'/{account}'/{role}/{index}";
}
