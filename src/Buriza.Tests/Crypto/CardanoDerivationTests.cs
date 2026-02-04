using Buriza.Core.Crypto;
using Buriza.Core.Providers;

namespace Buriza.Tests.Crypto;

public class CardanoDerivationTests
{
    [Fact]
    public void Purpose_IsShelleyPurpose()
    {
        // Assert - CIP-1852 Shelley purpose
        Assert.Equal(1852, CardanoDerivation.Purpose);
    }

    [Fact]
    public void CoinType_IsAdaCoinType()
    {
        // Assert - SLIP-44 ADA coin type
        Assert.Equal(1815, CardanoDerivation.CoinType);
    }

    [Fact]
    public void GetPath_WithDefaultValues_ReturnsCorrectPath()
    {
        // Act
        string path = CardanoDerivation.GetPath(0, 0, 0);

        // Assert - m/1852'/1815'/0'/0/0
        Assert.Equal("m/1852'/1815'/0'/0/0", path);
    }

    [Fact]
    public void GetPath_WithDifferentAccount_ReturnsCorrectPath()
    {
        // Act
        string path = CardanoDerivation.GetPath(1, 0, 0);

        // Assert
        Assert.Equal("m/1852'/1815'/1'/0/0", path);
    }

    [Fact]
    public void GetPath_WithInternalRole_ReturnsCorrectPath()
    {
        // Act - Role 1 is internal/change addresses
        string path = CardanoDerivation.GetPath(0, 1, 0);

        // Assert
        Assert.Equal("m/1852'/1815'/0'/1/0", path);
    }

    [Fact]
    public void GetPath_WithStakingRole_ReturnsCorrectPath()
    {
        // Act - Role 2 is staking
        string path = CardanoDerivation.GetPath(0, 2, 0);

        // Assert
        Assert.Equal("m/1852'/1815'/0'/2/0", path);
    }

    [Fact]
    public void GetPath_WithDifferentIndex_ReturnsCorrectPath()
    {
        // Act
        string path = CardanoDerivation.GetPath(0, 0, 5);

        // Assert
        Assert.Equal("m/1852'/1815'/0'/0/5", path);
    }

    [Fact]
    public void GetPath_WithAllDifferentValues_ReturnsCorrectPath()
    {
        // Act
        string path = CardanoDerivation.GetPath(2, 1, 10);

        // Assert
        Assert.Equal("m/1852'/1815'/2'/1/10", path);
    }

    [Theory]
    [InlineData(0, 0, 0, "m/1852'/1815'/0'/0/0")]
    [InlineData(0, 0, 1, "m/1852'/1815'/0'/0/1")]
    [InlineData(0, 1, 0, "m/1852'/1815'/0'/1/0")]
    [InlineData(1, 0, 0, "m/1852'/1815'/1'/0/0")]
    [InlineData(5, 2, 100, "m/1852'/1815'/5'/2/100")]
    public void GetPath_VariousCombinations_ReturnsExpectedPath(int account, int role, int index, string expected)
    {
        // Act
        string path = CardanoDerivation.GetPath(account, role, index);

        // Assert
        Assert.Equal(expected, path);
    }

    [Fact]
    public void GetPath_AccountIsHardened()
    {
        // Act
        string path = CardanoDerivation.GetPath(0, 0, 0);

        // Assert - Account level should have hardened marker (')
        Assert.Contains("'/0'/0/0", path);
    }

    [Fact]
    public void GetPath_RoleAndIndexAreNotHardened()
    {
        // Act
        string path = CardanoDerivation.GetPath(0, 0, 0);

        // Assert - Role and index should NOT have hardened marker
        // Path ends with /0/0 (not /0'/0')
        Assert.EndsWith("/0/0", path);
    }

    [Fact]
    public void GetPath_StartsWithMasterNode()
    {
        // Act
        string path = CardanoDerivation.GetPath(0, 0, 0);

        // Assert
        Assert.StartsWith("m/", path);
    }
}

public class KeyDerivationOptionsTests
{
    [Fact]
    public void Algorithm_IsArgon2id()
    {
        // RFC 9106 recommended algorithm
        Assert.Equal("Argon2id", KeyDerivationOptions.Default.Algorithm);
    }

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
    public void Encryption_IsAESGCM()
    {
        Assert.Equal("AES-GCM", KeyDerivationOptions.Default.Encryption);
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

    [Fact]
    public void Minimum_HasOWASPMinimumValues()
    {
        // OWASP minimum for constrained environments
        Assert.Equal(19456, KeyDerivationOptions.Minimum.MemoryCost);  // 19 MiB
        Assert.Equal(2, KeyDerivationOptions.Minimum.TimeCost);
        Assert.Equal(1, KeyDerivationOptions.Minimum.Parallelism);
    }
}
