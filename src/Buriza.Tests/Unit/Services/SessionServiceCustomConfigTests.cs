using Buriza.Core.Services;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

using ChainRegistryData = Buriza.Data.Models.Common.ChainRegistry;

namespace Buriza.Tests.Unit.Services;

/// <summary>
/// Tests for SessionService custom provider config caching (API keys and endpoints).
/// </summary>
public class SessionServiceCustomConfigTests : IDisposable
{
    private readonly SessionService _session = new();

    public void Dispose() => _session.Dispose();

    #region Custom API Key Cache Tests

    [Fact]
    public void SetCustomApiKey_StoresApiKey()
    {
        // Arrange
        const string apiKey = "test-api-key-123";

        // Act
        _session.SetCustomApiKey(ChainRegistryData.CardanoMainnet, apiKey);
        string? cached = _session.GetCustomApiKey(ChainRegistryData.CardanoMainnet);

        // Assert
        Assert.Equal(apiKey, cached);
    }

    [Fact]
    public void GetCustomApiKey_WhenNotSet_ReturnsNull()
    {
        // Act
        string? cached = _session.GetCustomApiKey(ChainRegistryData.CardanoMainnet);

        // Assert
        Assert.Null(cached);
    }

    [Fact]
    public void HasCustomApiKey_WhenSet_ReturnsTrue()
    {
        // Arrange
        _session.SetCustomApiKey(ChainRegistryData.CardanoMainnet, "test-key");

        // Act & Assert
        Assert.True(_session.HasCustomApiKey(ChainRegistryData.CardanoMainnet));
    }

    [Fact]
    public void HasCustomApiKey_WhenNotSet_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(_session.HasCustomApiKey(ChainRegistryData.CardanoMainnet));
    }

    [Fact]
    public void ClearCustomApiKey_RemovesApiKey()
    {
        // Arrange
        _session.SetCustomApiKey(ChainRegistryData.CardanoMainnet, "test-key");
        Assert.True(_session.HasCustomApiKey(ChainRegistryData.CardanoMainnet));

        // Act
        _session.ClearCustomApiKey(ChainRegistryData.CardanoMainnet);

        // Assert
        Assert.False(_session.HasCustomApiKey(ChainRegistryData.CardanoMainnet));
        Assert.Null(_session.GetCustomApiKey(ChainRegistryData.CardanoMainnet));
    }

    [Fact]
    public void SetCustomApiKey_DifferentiatesByChain()
    {
        // Arrange
        const string cardanoKey = "cardano-key";
        const string otherKey = "other-chain-key";
        ChainInfo mockChain1 = new() { Chain = (ChainType)1, Network = NetworkType.Mainnet, Symbol = "TEST", Name = "Test", Decimals = 6 };

        // Act
        _session.SetCustomApiKey(ChainRegistryData.CardanoMainnet, cardanoKey);
        _session.SetCustomApiKey(mockChain1, otherKey);

        // Assert
        Assert.Equal(cardanoKey, _session.GetCustomApiKey(ChainRegistryData.CardanoMainnet));
        Assert.Equal(otherKey, _session.GetCustomApiKey(mockChain1));
    }

    [Fact]
    public void SetCustomApiKey_DifferentiatesByNetwork()
    {
        // Arrange
        const string mainnetKey = "mainnet-key";
        const string preprodKey = "preprod-key";
        const string previewKey = "preview-key";

        // Act
        _session.SetCustomApiKey(ChainRegistryData.CardanoMainnet, mainnetKey);
        _session.SetCustomApiKey(ChainRegistryData.CardanoPreprod, preprodKey);
        _session.SetCustomApiKey(ChainRegistryData.CardanoPreview, previewKey);

        // Assert
        Assert.Equal(mainnetKey, _session.GetCustomApiKey(ChainRegistryData.CardanoMainnet));
        Assert.Equal(preprodKey, _session.GetCustomApiKey(ChainRegistryData.CardanoPreprod));
        Assert.Equal(previewKey, _session.GetCustomApiKey(ChainRegistryData.CardanoPreview));
    }

    [Fact]
    public void SetCustomApiKey_OverwritesExistingKey()
    {
        // Arrange
        _session.SetCustomApiKey(ChainRegistryData.CardanoMainnet, "original-key");

        // Act
        _session.SetCustomApiKey(ChainRegistryData.CardanoMainnet, "new-key");

        // Assert
        Assert.Equal("new-key", _session.GetCustomApiKey(ChainRegistryData.CardanoMainnet));
    }

    [Fact]
    public void ClearCustomApiKey_WhenNotSet_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        _session.ClearCustomApiKey(ChainRegistryData.CardanoMainnet);
    }

    #endregion

    #region Custom Endpoint Cache Tests

    [Fact]
    public void SetCustomEndpoint_StoresEndpoint()
    {
        // Arrange
        const string endpoint = "https://custom.endpoint.com/api";

        // Act
        _session.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, endpoint);
        string? cached = _session.GetCustomEndpoint(ChainRegistryData.CardanoMainnet);

        // Assert
        Assert.Equal(endpoint, cached);
    }

    [Fact]
    public void GetCustomEndpoint_WhenNotSet_ReturnsNull()
    {
        // Act
        string? cached = _session.GetCustomEndpoint(ChainRegistryData.CardanoMainnet);

        // Assert
        Assert.Null(cached);
    }

    [Fact]
    public void ClearCustomEndpoint_RemovesEndpoint()
    {
        // Arrange
        _session.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, "https://test.com");

        // Act
        _session.ClearCustomEndpoint(ChainRegistryData.CardanoMainnet);

        // Assert
        Assert.Null(_session.GetCustomEndpoint(ChainRegistryData.CardanoMainnet));
    }

    [Fact]
    public void SetCustomEndpoint_DifferentiatesByNetwork()
    {
        // Arrange
        const string mainnetEndpoint = "https://mainnet.example.com";
        const string preprodEndpoint = "https://preprod.example.com";

        // Act
        _session.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, mainnetEndpoint);
        _session.SetCustomEndpoint(ChainRegistryData.CardanoPreprod, preprodEndpoint);

        // Assert
        Assert.Equal(mainnetEndpoint, _session.GetCustomEndpoint(ChainRegistryData.CardanoMainnet));
        Assert.Equal(preprodEndpoint, _session.GetCustomEndpoint(ChainRegistryData.CardanoPreprod));
    }

    [Fact]
    public void SetCustomEndpoint_OverwritesExistingEndpoint()
    {
        // Arrange
        _session.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, "https://old.endpoint.com");

        // Act
        _session.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, "https://new.endpoint.com");

        // Assert
        Assert.Equal("https://new.endpoint.com", _session.GetCustomEndpoint(ChainRegistryData.CardanoMainnet));
    }

    #endregion

    #region ClearCache Tests

    [Fact]
    public void ClearCache_ClearsApiKeys()
    {
        // Arrange
        _session.SetCustomApiKey(ChainRegistryData.CardanoMainnet, "key1");
        _session.SetCustomApiKey(ChainRegistryData.CardanoPreprod, "key2");

        // Act
        _session.ClearCache();

        // Assert
        Assert.Null(_session.GetCustomApiKey(ChainRegistryData.CardanoMainnet));
        Assert.Null(_session.GetCustomApiKey(ChainRegistryData.CardanoPreprod));
    }

    [Fact]
    public void ClearCache_ClearsEndpoints()
    {
        // Arrange
        _session.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, "https://test1.com");
        _session.SetCustomEndpoint(ChainRegistryData.CardanoPreprod, "https://test2.com");

        // Act
        _session.ClearCache();

        // Assert
        Assert.Null(_session.GetCustomEndpoint(ChainRegistryData.CardanoMainnet));
        Assert.Null(_session.GetCustomEndpoint(ChainRegistryData.CardanoPreprod));
    }

    [Fact]
    public void ClearCache_ClearsAllCaches()
    {
        // Arrange
        _session.CacheAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false, "addr_test");
        _session.SetCustomApiKey(ChainRegistryData.CardanoMainnet, "test-key");
        _session.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, "https://test.com");

        // Act
        _session.ClearCache();

        // Assert
        Assert.Null(_session.GetCachedAddress(1, ChainRegistryData.CardanoMainnet, 0, 0, false));
        Assert.Null(_session.GetCustomApiKey(ChainRegistryData.CardanoMainnet));
        Assert.Null(_session.GetCustomEndpoint(ChainRegistryData.CardanoMainnet));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ClearsApiKeyCache()
    {
        // Arrange
        SessionService session = new();
        session.SetCustomApiKey(ChainRegistryData.CardanoMainnet, "test-key");

        // Act
        session.Dispose();

        // Assert
        Assert.Null(session.GetCustomApiKey(ChainRegistryData.CardanoMainnet));
    }

    [Fact]
    public void Dispose_ClearsEndpointCache()
    {
        // Arrange
        SessionService session = new();
        session.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, "https://test.com");

        // Act
        session.Dispose();

        // Assert
        Assert.Null(session.GetCustomEndpoint(ChainRegistryData.CardanoMainnet));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void SetCustomApiKey_WithEmptyString_Works()
    {
        // Act
        _session.SetCustomApiKey(ChainRegistryData.CardanoMainnet, "");

        // Assert
        Assert.Equal("", _session.GetCustomApiKey(ChainRegistryData.CardanoMainnet));
    }

    [Fact]
    public void SetCustomEndpoint_WithEmptyString_Works()
    {
        // Act
        _session.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, "");

        // Assert
        Assert.Equal("", _session.GetCustomEndpoint(ChainRegistryData.CardanoMainnet));
    }

    [Fact]
    public void ApiKeyAndEndpoint_IndependentPerChainNetwork()
    {
        // Arrange & Act
        _session.SetCustomApiKey(ChainRegistryData.CardanoMainnet, "mainnet-key");
        _session.SetCustomEndpoint(ChainRegistryData.CardanoPreprod, "https://preprod.com");

        // Assert - Should not cross-pollinate
        Assert.Equal("mainnet-key", _session.GetCustomApiKey(ChainRegistryData.CardanoMainnet));
        Assert.Null(_session.GetCustomEndpoint(ChainRegistryData.CardanoMainnet));
        Assert.Null(_session.GetCustomApiKey(ChainRegistryData.CardanoPreprod));
        Assert.Equal("https://preprod.com", _session.GetCustomEndpoint(ChainRegistryData.CardanoPreprod));
    }

    #endregion
}
