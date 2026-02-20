using Buriza.Core.Crypto;
using Chrysalis.Wallet.Models.Enums;

namespace Buriza.Tests.Unit.Crypto;

/// <summary>
/// Tests for CIP-1852 Cardano derivation path constants from Chrysalis.Wallet.
/// Verifies that the library uses correct values per specification.
/// </summary>
public class CardanoDerivationTests
{
    [Fact]
    public void PurposeType_Shelley_IsCip1852Purpose()
    {
        // CIP-1852 Shelley purpose = 1852
        Assert.Equal(1852, (int)PurposeType.Shelley);
    }

    [Fact]
    public void CoinType_Ada_IsSlip44CoinType()
    {
        // SLIP-44 ADA coin type = 1815
        Assert.Equal(1815, (int)CoinType.Ada);
    }

    [Fact]
    public void RoleType_ExternalChain_IsZero()
    {
        // External/payment addresses = role 0
        Assert.Equal(0, (int)RoleType.ExternalChain);
    }

    [Fact]
    public void RoleType_InternalChain_IsOne()
    {
        // Internal/change addresses = role 1
        Assert.Equal(1, (int)RoleType.InternalChain);
    }

    [Fact]
    public void RoleType_Staking_IsTwo()
    {
        // Staking addresses = role 2
        Assert.Equal(2, (int)RoleType.Staking);
    }

    [Theory]
    [InlineData(PurposeType.Shelley, 1852)]
    [InlineData(PurposeType.MultiSig, 1854)]
    public void PurposeType_HasCorrectValues(PurposeType purpose, int expected)
    {
        Assert.Equal(expected, (int)purpose);
    }

    [Theory]
    [InlineData(RoleType.ExternalChain, 0)]
    [InlineData(RoleType.InternalChain, 1)]
    [InlineData(RoleType.Staking, 2)]
    public void RoleType_HasCorrectValues(RoleType role, int expected)
    {
        Assert.Equal(expected, (int)role);
    }
}

public class KeyDerivationOptionsTests
{
    [Fact]
    public void MemoryCost_Is64MiB()
    {
        // RFC 9106 second recommendation, Bitwarden default
        Assert.Equal(65536, KeyDerivationOptions.Default.MemoryCost);
    }

    [Fact]
    public void TimeCost_Is3Iterations()
    {
        // RFC 9106 second recommendation
        Assert.Equal(3, KeyDerivationOptions.Default.TimeCost);
    }

    [Fact]
    public void Parallelism_Is4Lanes()
    {
        // RFC 9106 recommendation
        Assert.Equal(4, KeyDerivationOptions.Default.Parallelism);
    }

    [Fact]
    public void KeyLength_Is256Bits()
    {
        Assert.Equal(256, KeyDerivationOptions.Default.KeyLength);
    }

    [Fact]
    public void SaltSize_Is32Bytes()
    {
        // RFC 9106 minimum is 16, we use 32 for extra security
        Assert.Equal(32, KeyDerivationOptions.Default.SaltSize);
    }

    [Fact]
    public void IvSize_Is12Bytes()
    {
        // Optimal for AES-GCM
        Assert.Equal(12, KeyDerivationOptions.Default.IvSize);
    }

    [Fact]
    public void TagSize_Is16Bytes()
    {
        // 128-bit authentication tag
        Assert.Equal(16, KeyDerivationOptions.Default.TagSize);
    }

    [Fact]
    public void KeyLengthInBytes_MatchesSaltSize()
    {
        // 256 bits = 32 bytes
        Assert.Equal(KeyDerivationOptions.Default.SaltSize, KeyDerivationOptions.Default.KeyLength / 8);
    }
}
