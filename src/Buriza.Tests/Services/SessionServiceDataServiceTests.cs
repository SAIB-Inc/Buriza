using Buriza.Core.Services;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

using ChainRegistryData = Buriza.Data.Models.Common.ChainRegistry;

namespace Buriza.Tests.Services;

/// <summary>
/// Tests for SessionService data service config caching (endpoints and API keys for TokenMetadata, TokenPrice, etc.).
/// </summary>
public class SessionServiceDataServiceTests : IDisposable
{
    private readonly SessionService _session = new();

    public void Dispose() => _session.Dispose();

    #region Data Service Endpoint Cache Tests

    [Fact]
    public void SetDataServiceEndpoint_StoresEndpoint()
    {
        // Arrange
        const string endpoint = "https://custom.tokenmetadata.com/api";

        // Act
        _session.SetDataServiceEndpoint(DataServiceType.TokenMetadata, endpoint);
        string? cached = _session.GetDataServiceEndpoint(DataServiceType.TokenMetadata);

        // Assert
        Assert.Equal(endpoint, cached);
    }

    [Fact]
    public void GetDataServiceEndpoint_WhenNotSet_ReturnsNull()
    {
        // Act
        string? cached = _session.GetDataServiceEndpoint(DataServiceType.TokenMetadata);

        // Assert
        Assert.Null(cached);
    }

    [Fact]
    public void ClearDataServiceEndpoint_RemovesEndpoint()
    {
        // Arrange
        _session.SetDataServiceEndpoint(DataServiceType.TokenMetadata, "https://test.com");
        Assert.NotNull(_session.GetDataServiceEndpoint(DataServiceType.TokenMetadata));

        // Act
        _session.ClearDataServiceEndpoint(DataServiceType.TokenMetadata);

        // Assert
        Assert.Null(_session.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
    }

    [Fact]
    public void SetDataServiceEndpoint_DifferentiatesByServiceType()
    {
        // Arrange
        const string metadataEndpoint = "https://metadata.example.com";
        const string priceEndpoint = "https://price.example.com";

        // Act
        _session.SetDataServiceEndpoint(DataServiceType.TokenMetadata, metadataEndpoint);
        _session.SetDataServiceEndpoint(DataServiceType.TokenPrice, priceEndpoint);

        // Assert
        Assert.Equal(metadataEndpoint, _session.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Equal(priceEndpoint, _session.GetDataServiceEndpoint(DataServiceType.TokenPrice));
    }

    [Fact]
    public void SetDataServiceEndpoint_OverwritesExistingEndpoint()
    {
        // Arrange
        _session.SetDataServiceEndpoint(DataServiceType.TokenMetadata, "https://old.endpoint.com");

        // Act
        _session.SetDataServiceEndpoint(DataServiceType.TokenMetadata, "https://new.endpoint.com");

        // Assert
        Assert.Equal("https://new.endpoint.com", _session.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
    }

    [Fact]
    public void ClearDataServiceEndpoint_WhenNotSet_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        _session.ClearDataServiceEndpoint(DataServiceType.TokenMetadata);
    }

    #endregion

    #region Data Service API Key Cache Tests

    [Fact]
    public void SetDataServiceApiKey_StoresApiKey()
    {
        // Arrange
        const string apiKey = "test-api-key-123";

        // Act
        _session.SetDataServiceApiKey(DataServiceType.TokenPrice, apiKey);
        string? cached = _session.GetDataServiceApiKey(DataServiceType.TokenPrice);

        // Assert
        Assert.Equal(apiKey, cached);
    }

    [Fact]
    public void GetDataServiceApiKey_WhenNotSet_ReturnsNull()
    {
        // Act
        string? cached = _session.GetDataServiceApiKey(DataServiceType.TokenPrice);

        // Assert
        Assert.Null(cached);
    }

    [Fact]
    public void ClearDataServiceApiKey_RemovesApiKey()
    {
        // Arrange
        _session.SetDataServiceApiKey(DataServiceType.TokenPrice, "test-key");
        Assert.NotNull(_session.GetDataServiceApiKey(DataServiceType.TokenPrice));

        // Act
        _session.ClearDataServiceApiKey(DataServiceType.TokenPrice);

        // Assert
        Assert.Null(_session.GetDataServiceApiKey(DataServiceType.TokenPrice));
    }

    [Fact]
    public void SetDataServiceApiKey_DifferentiatesByServiceType()
    {
        // Arrange
        const string metadataKey = "metadata-api-key";
        const string priceKey = "price-api-key";

        // Act
        _session.SetDataServiceApiKey(DataServiceType.TokenMetadata, metadataKey);
        _session.SetDataServiceApiKey(DataServiceType.TokenPrice, priceKey);

        // Assert
        Assert.Equal(metadataKey, _session.GetDataServiceApiKey(DataServiceType.TokenMetadata));
        Assert.Equal(priceKey, _session.GetDataServiceApiKey(DataServiceType.TokenPrice));
    }

    [Fact]
    public void SetDataServiceApiKey_OverwritesExistingKey()
    {
        // Arrange
        _session.SetDataServiceApiKey(DataServiceType.TokenPrice, "original-key");

        // Act
        _session.SetDataServiceApiKey(DataServiceType.TokenPrice, "new-key");

        // Assert
        Assert.Equal("new-key", _session.GetDataServiceApiKey(DataServiceType.TokenPrice));
    }

    [Fact]
    public void ClearDataServiceApiKey_WhenNotSet_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        _session.ClearDataServiceApiKey(DataServiceType.TokenPrice);
    }

    #endregion

    #region ClearCache Tests

    [Fact]
    public void ClearCache_ClearsDataServiceEndpoints()
    {
        // Arrange
        _session.SetDataServiceEndpoint(DataServiceType.TokenMetadata, "https://test1.com");
        _session.SetDataServiceEndpoint(DataServiceType.TokenPrice, "https://test2.com");

        // Act
        _session.ClearCache();

        // Assert
        Assert.Null(_session.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Null(_session.GetDataServiceEndpoint(DataServiceType.TokenPrice));
    }

    [Fact]
    public void ClearCache_ClearsDataServiceApiKeys()
    {
        // Arrange
        _session.SetDataServiceApiKey(DataServiceType.TokenMetadata, "key1");
        _session.SetDataServiceApiKey(DataServiceType.TokenPrice, "key2");

        // Act
        _session.ClearCache();

        // Assert
        Assert.Null(_session.GetDataServiceApiKey(DataServiceType.TokenMetadata));
        Assert.Null(_session.GetDataServiceApiKey(DataServiceType.TokenPrice));
    }

    [Fact]
    public void ClearCache_ClearsAllDataServiceConfigs()
    {
        // Arrange
        _session.SetDataServiceEndpoint(DataServiceType.TokenMetadata, "https://metadata.com");
        _session.SetDataServiceApiKey(DataServiceType.TokenMetadata, "metadata-key");
        _session.SetDataServiceEndpoint(DataServiceType.TokenPrice, "https://price.com");
        _session.SetDataServiceApiKey(DataServiceType.TokenPrice, "price-key");

        // Act
        _session.ClearCache();

        // Assert
        Assert.Null(_session.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Null(_session.GetDataServiceApiKey(DataServiceType.TokenMetadata));
        Assert.Null(_session.GetDataServiceEndpoint(DataServiceType.TokenPrice));
        Assert.Null(_session.GetDataServiceApiKey(DataServiceType.TokenPrice));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ClearsDataServiceEndpointCache()
    {
        // Arrange
        SessionService session = new();
        session.SetDataServiceEndpoint(DataServiceType.TokenMetadata, "https://test.com");

        // Act
        session.Dispose();

        // Assert
        Assert.Null(session.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
    }

    [Fact]
    public void Dispose_ClearsDataServiceApiKeyCache()
    {
        // Arrange
        SessionService session = new();
        session.SetDataServiceApiKey(DataServiceType.TokenPrice, "test-key");

        // Act
        session.Dispose();

        // Assert
        Assert.Null(session.GetDataServiceApiKey(DataServiceType.TokenPrice));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void SetDataServiceEndpoint_WithEmptyString_Works()
    {
        // Act
        _session.SetDataServiceEndpoint(DataServiceType.TokenMetadata, "");

        // Assert
        Assert.Equal("", _session.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
    }

    [Fact]
    public void SetDataServiceApiKey_WithEmptyString_Works()
    {
        // Act
        _session.SetDataServiceApiKey(DataServiceType.TokenPrice, "");

        // Assert
        Assert.Equal("", _session.GetDataServiceApiKey(DataServiceType.TokenPrice));
    }

    [Fact]
    public void DataServiceEndpointAndApiKey_IndependentPerServiceType()
    {
        // Arrange & Act
        _session.SetDataServiceApiKey(DataServiceType.TokenMetadata, "metadata-key");
        _session.SetDataServiceEndpoint(DataServiceType.TokenPrice, "https://price.com");

        // Assert - Should not cross-pollinate
        Assert.Equal("metadata-key", _session.GetDataServiceApiKey(DataServiceType.TokenMetadata));
        Assert.Null(_session.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Null(_session.GetDataServiceApiKey(DataServiceType.TokenPrice));
        Assert.Equal("https://price.com", _session.GetDataServiceEndpoint(DataServiceType.TokenPrice));
    }

    [Fact]
    public void DataServiceCache_IndependentFromChainProviderCache()
    {
        // Arrange & Act - Set data service config
        _session.SetDataServiceEndpoint(DataServiceType.TokenMetadata, "https://data.service.com");
        _session.SetDataServiceApiKey(DataServiceType.TokenMetadata, "data-key");

        // Set chain provider config
        _session.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, "https://chain.provider.com");
        _session.SetCustomApiKey(ChainRegistryData.CardanoMainnet, "chain-key");

        // Assert - Should be completely independent
        Assert.Equal("https://data.service.com", _session.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Equal("data-key", _session.GetDataServiceApiKey(DataServiceType.TokenMetadata));
        Assert.Equal("https://chain.provider.com", _session.GetCustomEndpoint(ChainRegistryData.CardanoMainnet));
        Assert.Equal("chain-key", _session.GetCustomApiKey(ChainRegistryData.CardanoMainnet));
    }

    [Fact]
    public void DataServiceCache_IndependentFromAddressCache()
    {
        // Arrange & Act
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, "addr_test");
        _session.SetDataServiceEndpoint(DataServiceType.TokenMetadata, "https://test.com");
        _session.SetDataServiceApiKey(DataServiceType.TokenMetadata, "test-key");

        // Assert - All independent
        Assert.Equal("addr_test", _session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false));
        Assert.Equal("https://test.com", _session.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Equal("test-key", _session.GetDataServiceApiKey(DataServiceType.TokenMetadata));
    }

    [Fact]
    public void ClearWalletCache_DoesNotAffectDataServiceCache()
    {
        // Arrange
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, "addr_test");
        _session.SetDataServiceEndpoint(DataServiceType.TokenMetadata, "https://test.com");
        _session.SetDataServiceApiKey(DataServiceType.TokenMetadata, "test-key");

        // Act - Clear wallet cache only
        _session.ClearWalletCache(1);

        // Assert - Wallet cache cleared, data service cache intact
        Assert.Null(_session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false));
        Assert.Equal("https://test.com", _session.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Equal("test-key", _session.GetDataServiceApiKey(DataServiceType.TokenMetadata));
    }

    [Theory]
    [InlineData(DataServiceType.TokenMetadata)]
    [InlineData(DataServiceType.TokenPrice)]
    public void AllDataServiceTypes_CanStoreAndRetrieve(DataServiceType serviceType)
    {
        // Arrange
        string endpoint = $"https://{serviceType}.example.com";
        string apiKey = $"{serviceType}-api-key";

        // Act
        _session.SetDataServiceEndpoint(serviceType, endpoint);
        _session.SetDataServiceApiKey(serviceType, apiKey);

        // Assert
        Assert.Equal(endpoint, _session.GetDataServiceEndpoint(serviceType));
        Assert.Equal(apiKey, _session.GetDataServiceApiKey(serviceType));
    }

    #endregion
}
