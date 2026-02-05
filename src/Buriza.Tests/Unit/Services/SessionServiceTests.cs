using Buriza.Core.Interfaces;
using Buriza.Core.Services;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

using ChainRegistryData = Buriza.Data.Models.Common.ChainRegistry;

namespace Buriza.Tests.Unit.Services;

public class SessionServiceTests : IDisposable
{
    private readonly ISessionService _session = new SessionService();
    private static readonly ChainInfo _mockChain1 = new() { Chain = (ChainType)1, Network = NetworkType.Mainnet, Symbol = "TEST", Name = "Test", Decimals = 6 };

    public void Dispose() => _session.Dispose();

    #region Address Cache Tests

    [Fact]
    public void CacheAddress_StoresAddress()
    {
        // Arrange
        const string address = "addr_test1qz2fxv2umyhttkxyxp8x0dlpdt3k6cwng5pxj3jhsydzer3jcu5d8ps7zex2k2xt3uqxgjqnnj83ws8lhrn648jjxtwq2ytjqp";

        // Act
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, address);
        string? cached = _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false);

        // Assert
        Assert.Equal(address, cached);
    }

    [Fact]
    public void GetCachedAddress_WhenNotCached_ReturnsNull()
    {
        // Act
        string? cached = _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false);

        // Assert
        Assert.Null(cached);
    }

    [Fact]
    public void CacheAddress_DifferentiatesExternalAndInternal()
    {
        // Arrange
        const string externalAddr = "addr_external";
        const string internalAddr = "addr_internal";

        // Act
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, externalAddr);  // external
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, true, internalAddr);   // internal (change)

        // Assert
        Assert.Equal(externalAddr, _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false));
        Assert.Equal(internalAddr, _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, true));
    }

    [Fact]
    public void CacheAddress_DifferentiatesByWalletId()
    {
        // Arrange
        const string addr1 = "addr_wallet1";
        const string addr2 = "addr_wallet2";

        // Act
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, addr1);
        _session.CacheAddress(2, ChainRegistryData.CardanoMainnet, 0, 0, false, addr2);

        // Assert
        Assert.Equal(addr1, _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false));
        Assert.Equal(addr2, _session.GetCachedAddress(2, ChainRegistryData.CardanoMainnet, 0, 0, false));
    }

    [Fact]
    public void CacheAddress_DifferentiatesByChain()
    {
        // Arrange - Using Cardano with different mock "chain" values
        // This tests the chain differentiation logic without requiring other chains to be implemented
        const string chain0Addr = "addr_chain0";
        const string chain1Addr = "addr_chain1";

        // Act
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, chain0Addr);
        _session.CacheAddress(1, _mockChain1, 0, 0, false, chain1Addr);

        // Assert
        Assert.Equal(chain0Addr, _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false));
        Assert.Equal(chain1Addr, _session.GetCachedAddress(1, _mockChain1, 0, 0, false));
    }

    [Fact]
    public void CacheAddress_DifferentiatesByAccountIndex()
    {
        // Arrange
        const string addr0 = "addr_account0";
        const string addr1 = "addr_account1";

        // Act
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, addr0);
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 1, 0, false, addr1);

        // Assert
        Assert.Equal(addr0, _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false));
        Assert.Equal(addr1, _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 1, 0, false));
    }

    [Fact]
    public void CacheAddress_DifferentiatesByAddressIndex()
    {
        // Arrange
        const string addr0 = "addr_index0";
        const string addr1 = "addr_index1";

        // Act
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, addr0);
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 1, false, addr1);

        // Assert
        Assert.Equal(addr0, _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false));
        Assert.Equal(addr1, _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 1, false));
    }

    [Fact]
    public void ClearCache_ClearsAllAddresses()
    {
        // Arrange
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, "addr_test");
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 1, false, "addr_test2");
        _session.CacheAddress(2, ChainRegistryData.CardanoMainnet, 0, 0, false, "addr_test3");

        // Act
        _session.ClearCache();

        // Assert
        Assert.Null(_session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false));
        Assert.Null(_session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 1, false));
        Assert.Null(_session.GetCachedAddress(2, ChainRegistryData.CardanoMainnet, 0, 0, false));
    }

    [Fact]
    public void HasCachedAddresses_WhenEmpty_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(_session.HasCachedAddresses(1, ChainRegistryData.CardanoMainnet, 0));
    }

    [Fact]
    public void HasCachedAddresses_WhenAddressesCached_ReturnsTrue()
    {
        // Arrange
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, "addr_test");

        // Act & Assert
        Assert.True(_session.HasCachedAddresses(1, ChainRegistryData.CardanoMainnet, 0));
    }

    [Fact]
    public void HasCachedAddresses_DifferentiatesByWalletChainAndAccount()
    {
        // Arrange
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, "addr_test");

        // Act & Assert
        Assert.True(_session.HasCachedAddresses(1, ChainRegistryData.CardanoMainnet, 0));
        Assert.False(_session.HasCachedAddresses(1, ChainRegistryData.CardanoPreprod, 0)); // Different network
        Assert.False(_session.HasCachedAddresses(1, _mockChain1, 0));                      // Different chain
        Assert.False(_session.HasCachedAddresses(2, ChainRegistryData.CardanoMainnet, 0)); // Different wallet
    }

    [Fact]
    public void HasCachedAddresses_WorksWithInternalAddresses()
    {
        // Arrange - Only cache internal (change) address
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, true, "addr_change");

        // Act & Assert
        Assert.True(_session.HasCachedAddresses(1, ChainRegistryData.CardanoMainnet, 0));
    }

    #endregion

    #region IDisposable Tests

    [Fact]
    public void Dispose_ClearsAddressCache()
    {
        // Arrange
        SessionService session = new();
        session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, "addr_test1abc");

        // Act
        session.Dispose();

        // Assert - Cache should be cleared
        Assert.Null(session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        SessionService session = new();
        session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, "addr_test");

        // Act & Assert - Should not throw
        session.Dispose();
        session.Dispose();
        session.Dispose();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void CacheAddress_TryAddDoesNotOverwrite()
    {
        // Arrange
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, "addr_original");

        // Act - Cache same key with different value
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, "addr_new");

        // Assert - Original value should remain (TryAdd behavior)
        Assert.Equal("addr_original", _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false));
    }

    [Fact]
    public void CacheAddress_WithLargeIndices_Works()
    {
        // Arrange
        const string address = "addr_large_indices";

        // Act - Use large indices
        _session.CacheAddress(int.MaxValue, ChainRegistryData.CardanoMainnet, int.MaxValue, int.MaxValue, true, address);

        // Assert
        Assert.Equal(address, _session.GetCachedAddress(int.MaxValue, ChainRegistryData.CardanoMainnet, int.MaxValue, int.MaxValue, true));
    }

    [Fact]
    public void ClearCache_WhenAlreadyEmpty_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        _session.ClearCache();
        _session.ClearCache();
    }

    [Fact]
    public void CacheAddress_WithEmptyString_Works()
    {
        // Act
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, "");

        // Assert
        Assert.Equal("", _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false));
    }

    [Fact]
    public void CacheAddress_WithLongAddress_Works()
    {
        // Arrange - Create a very long address string
        string longAddress = new('a', 10000);

        // Act
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, longAddress);

        // Assert
        Assert.Equal(longAddress, _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false));
    }

    [Fact]
    public void CacheAddress_WithUnicodeAddress_Works()
    {
        // Arrange
        const string unicodeAddress = "addr_密码🔐Пароль";

        // Act
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, unicodeAddress);

        // Assert
        Assert.Equal(unicodeAddress, _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false));
    }

    [Fact]
    public void CacheAddress_MultipleAddressesPerAccount_Works()
    {
        // Arrange & Act - Cache 20 external and 20 internal addresses
        for (int i = 0; i < 20; i++)
        {
            _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, i, false, $"addr_ext_{i}");
            _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, i, true, $"addr_int_{i}");
        }

        // Assert
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal($"addr_ext_{i}", _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, i, false));
            Assert.Equal($"addr_int_{i}", _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, i, true));
        }
    }

    [Fact]
    public void HasCachedAddresses_AfterClearCache_ReturnsFalse()
    {
        // Arrange
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, "addr_test");
        Assert.True(_session.HasCachedAddresses(1, ChainRegistryData.CardanoMainnet, 0));

        // Act
        _session.ClearCache();

        // Assert
        Assert.False(_session.HasCachedAddresses(1, ChainRegistryData.CardanoMainnet, 0));
    }

    [Fact]
    public void CacheAddress_MultiChainSameWallet_Works()
    {
        // Arrange & Act - Cache addresses for multiple chains in same wallet
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, "chain0_addr_0");
        _session.CacheAddress(1, _mockChain1, 0, 0, false, "chain1_addr_0");
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 1, false, "chain0_addr_1");
        _session.CacheAddress(1, _mockChain1, 0, 1, false, "chain1_addr_1");

        // Assert - Each chain has its own addresses
        Assert.Equal("chain0_addr_0", _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false));
        Assert.Equal("chain1_addr_0", _session.GetCachedAddress(1, _mockChain1, 0, 0, false));
        Assert.Equal("chain0_addr_1", _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 1, false));
        Assert.Equal("chain1_addr_1", _session.GetCachedAddress(1, _mockChain1, 0, 1, false));

        // Assert - HasCachedAddresses works per chain
        Assert.True(_session.HasCachedAddresses(1, ChainRegistryData.CardanoMainnet, 0));
        Assert.True(_session.HasCachedAddresses(1, _mockChain1, 0));
    }

    #endregion
}
