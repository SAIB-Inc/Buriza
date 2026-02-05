using System.Security.Cryptography;
using Buriza.Core.Models;
using Buriza.Core.Services;
using Buriza.Data.Models.Enums;
using Buriza.Tests.Mocks;

namespace Buriza.Tests.Unit.Services;

/// <summary>
/// Tests for DataServiceRegistry - manages custom data service configurations
/// (TokenMetadata, TokenPrice endpoints and encrypted API keys).
/// </summary>
public class DataServiceRegistryTests : IDisposable
{
    private readonly InMemoryWalletStorage _storage;
    private readonly SessionService _sessionService;
    private readonly DataServiceRegistry _registry;

    private const string TestPassword = "TestPassword123!";

    public DataServiceRegistryTests()
    {
        _storage = new InMemoryWalletStorage();
        _sessionService = new SessionService();
        _registry = new DataServiceRegistry(_storage, _sessionService);
    }

    public void Dispose()
    {
        _sessionService.Dispose();
        GC.SuppressFinalize(this);
    }

    #region GetConfigAsync Tests

    [Fact]
    public async Task GetConfigAsync_WhenNotSet_ReturnsNull()
    {
        // Act
        DataServiceConfig? config = await _registry.GetConfigAsync(DataServiceType.TokenMetadata);

        // Assert
        Assert.Null(config);
    }

    [Fact]
    public async Task GetConfigAsync_AfterSet_ReturnsConfig()
    {
        // Arrange
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://custom.metadata.com",
            null,
            TestPassword,
            "Custom Metadata");

        // Act
        DataServiceConfig? config = await _registry.GetConfigAsync(DataServiceType.TokenMetadata);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(DataServiceType.TokenMetadata, config.ServiceType);
        Assert.Equal("https://custom.metadata.com", config.Endpoint);
        Assert.Equal("Custom Metadata", config.Name);
        Assert.False(config.HasCustomApiKey);
    }

    #endregion

    #region SetConfigAsync Tests

    [Fact]
    public async Task SetConfigAsync_SavesConfigToStorage()
    {
        // Act
        await _registry.SetConfigAsync(
            DataServiceType.TokenPrice,
            "https://custom.price.com",
            null,
            TestPassword);

        // Assert
        Assert.Equal(1, _storage.DataServiceConfigCount);
    }

    [Fact]
    public async Task SetConfigAsync_CachesEndpointInSession()
    {
        // Act
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://custom.endpoint.com",
            null,
            TestPassword);

        // Assert
        Assert.Equal("https://custom.endpoint.com", _sessionService.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
    }

    [Fact]
    public async Task SetConfigAsync_WithApiKey_EncryptsAndSaves()
    {
        // Act
        await _registry.SetConfigAsync(
            DataServiceType.TokenPrice,
            "https://custom.price.com",
            "my-secret-api-key",
            TestPassword);

        // Assert
        DataServiceConfig? config = await _registry.GetConfigAsync(DataServiceType.TokenPrice);
        Assert.NotNull(config);
        Assert.True(config.HasCustomApiKey);
    }

    [Fact]
    public async Task SetConfigAsync_WithApiKey_CachesInSession()
    {
        // Act
        await _registry.SetConfigAsync(
            DataServiceType.TokenPrice,
            "https://custom.price.com",
            "my-secret-api-key",
            TestPassword);

        // Assert
        Assert.Equal("my-secret-api-key", _sessionService.GetDataServiceApiKey(DataServiceType.TokenPrice));
    }

    [Fact]
    public async Task SetConfigAsync_WithInvalidUrl_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _registry.SetConfigAsync(
                DataServiceType.TokenMetadata,
                "not-a-valid-url",
                null,
                TestPassword));
    }

    [Fact]
    public async Task SetConfigAsync_WithNullEndpoint_Succeeds()
    {
        // Act - Only set API key, no endpoint
        await _registry.SetConfigAsync(
            DataServiceType.TokenPrice,
            null,
            "api-key-only",
            TestPassword);

        // Assert
        DataServiceConfig? config = await _registry.GetConfigAsync(DataServiceType.TokenPrice);
        Assert.NotNull(config);
        Assert.Null(config.Endpoint);
        Assert.True(config.HasCustomApiKey);
    }

    [Fact]
    public async Task SetConfigAsync_WithNullApiKey_ClearsExistingKey()
    {
        // Arrange - First set a config with API key
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://test.com",
            "initial-key",
            TestPassword);

        // Act - Update without API key
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://test.com",
            null,
            TestPassword);

        // Assert
        DataServiceConfig? config = await _registry.GetConfigAsync(DataServiceType.TokenMetadata);
        Assert.NotNull(config);
        Assert.False(config.HasCustomApiKey);
        Assert.Null(_sessionService.GetDataServiceApiKey(DataServiceType.TokenMetadata));
    }

    [Fact]
    public async Task SetConfigAsync_WithEmptyEndpoint_ClearsSessionEndpoint()
    {
        // Arrange - Set initial endpoint
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://initial.com",
            null,
            TestPassword);
        Assert.NotNull(_sessionService.GetDataServiceEndpoint(DataServiceType.TokenMetadata));

        // Act - Clear endpoint by setting null
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            null,
            null,
            TestPassword);

        // Assert
        Assert.Null(_sessionService.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
    }

    [Fact]
    public async Task SetConfigAsync_OverwritesExistingConfig()
    {
        // Arrange
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://old.endpoint.com",
            "old-key",
            TestPassword,
            "Old Name");

        // Act
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://new.endpoint.com",
            "new-key",
            TestPassword,
            "New Name");

        // Assert
        DataServiceConfig? config = await _registry.GetConfigAsync(DataServiceType.TokenMetadata);
        Assert.NotNull(config);
        Assert.Equal("https://new.endpoint.com", config.Endpoint);
        Assert.Equal("New Name", config.Name);
        Assert.Equal("https://new.endpoint.com", _sessionService.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Equal("new-key", _sessionService.GetDataServiceApiKey(DataServiceType.TokenMetadata));
    }

    [Fact]
    public async Task SetConfigAsync_DifferentServiceTypes_IndependentConfigs()
    {
        // Act
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://metadata.com",
            "metadata-key",
            TestPassword);

        await _registry.SetConfigAsync(
            DataServiceType.TokenPrice,
            "https://price.com",
            "price-key",
            TestPassword);

        // Assert
        DataServiceConfig? metadataConfig = await _registry.GetConfigAsync(DataServiceType.TokenMetadata);
        DataServiceConfig? priceConfig = await _registry.GetConfigAsync(DataServiceType.TokenPrice);

        Assert.NotNull(metadataConfig);
        Assert.NotNull(priceConfig);
        Assert.Equal("https://metadata.com", metadataConfig.Endpoint);
        Assert.Equal("https://price.com", priceConfig.Endpoint);
        Assert.Equal(2, _storage.DataServiceConfigCount);
    }

    #endregion

    #region ClearConfigAsync Tests

    [Fact]
    public async Task ClearConfigAsync_RemovesConfigFromStorage()
    {
        // Arrange
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://test.com",
            "test-key",
            TestPassword);

        // Act
        await _registry.ClearConfigAsync(DataServiceType.TokenMetadata);

        // Assert
        DataServiceConfig? config = await _registry.GetConfigAsync(DataServiceType.TokenMetadata);
        Assert.Null(config);
        Assert.Equal(0, _storage.DataServiceConfigCount);
    }

    [Fact]
    public async Task ClearConfigAsync_ClearsSessionCache()
    {
        // Arrange
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://test.com",
            "test-key",
            TestPassword);

        // Act
        await _registry.ClearConfigAsync(DataServiceType.TokenMetadata);

        // Assert
        Assert.Null(_sessionService.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Null(_sessionService.GetDataServiceApiKey(DataServiceType.TokenMetadata));
    }

    [Fact]
    public async Task ClearConfigAsync_WhenNotSet_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        await _registry.ClearConfigAsync(DataServiceType.TokenMetadata);
    }

    [Fact]
    public async Task ClearConfigAsync_OnlyAffectsSpecifiedServiceType()
    {
        // Arrange
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://metadata.com",
            "metadata-key",
            TestPassword);

        await _registry.SetConfigAsync(
            DataServiceType.TokenPrice,
            "https://price.com",
            "price-key",
            TestPassword);

        // Act - Clear only TokenMetadata
        await _registry.ClearConfigAsync(DataServiceType.TokenMetadata);

        // Assert - TokenPrice should remain
        Assert.Null(await _registry.GetConfigAsync(DataServiceType.TokenMetadata));
        Assert.NotNull(await _registry.GetConfigAsync(DataServiceType.TokenPrice));
        Assert.Equal("https://price.com", _sessionService.GetDataServiceEndpoint(DataServiceType.TokenPrice));
    }

    #endregion

    #region LoadConfigAsync Tests

    [Fact]
    public async Task LoadConfigAsync_LoadsEndpointIntoSession()
    {
        // Arrange - Save config then clear session
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://custom.com",
            null,
            TestPassword);

        _sessionService.ClearCache();
        Assert.Null(_sessionService.GetDataServiceEndpoint(DataServiceType.TokenMetadata));

        // Act
        await _registry.LoadConfigAsync(DataServiceType.TokenMetadata, TestPassword);

        // Assert
        Assert.Equal("https://custom.com", _sessionService.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
    }

    [Fact]
    public async Task LoadConfigAsync_DecryptsAndCachesApiKey()
    {
        // Arrange - Save config then clear session
        await _registry.SetConfigAsync(
            DataServiceType.TokenPrice,
            "https://price.com",
            "secret-api-key",
            TestPassword);

        _sessionService.ClearCache();
        Assert.Null(_sessionService.GetDataServiceApiKey(DataServiceType.TokenPrice));

        // Act
        await _registry.LoadConfigAsync(DataServiceType.TokenPrice, TestPassword);

        // Assert
        Assert.Equal("secret-api-key", _sessionService.GetDataServiceApiKey(DataServiceType.TokenPrice));
    }

    [Fact]
    public async Task LoadConfigAsync_WithWrongPassword_ThrowsCryptographicException()
    {
        // Arrange
        await _registry.SetConfigAsync(
            DataServiceType.TokenPrice,
            "https://price.com",
            "secret-api-key",
            TestPassword);

        _sessionService.ClearCache();

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(() =>
            _registry.LoadConfigAsync(DataServiceType.TokenPrice, "WrongPassword"));
    }

    [Fact]
    public async Task LoadConfigAsync_WithNoConfig_DoesNothing()
    {
        // Act - Should not throw
        await _registry.LoadConfigAsync(DataServiceType.TokenMetadata, TestPassword);

        // Assert
        Assert.Null(_sessionService.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Null(_sessionService.GetDataServiceApiKey(DataServiceType.TokenMetadata));
    }

    [Fact]
    public async Task LoadConfigAsync_WithEndpointOnly_LoadsEndpoint()
    {
        // Arrange - Config with endpoint but no API key
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://public-api.com",
            null,
            TestPassword);

        _sessionService.ClearCache();

        // Act
        await _registry.LoadConfigAsync(DataServiceType.TokenMetadata, TestPassword);

        // Assert
        Assert.Equal("https://public-api.com", _sessionService.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Null(_sessionService.GetDataServiceApiKey(DataServiceType.TokenMetadata));
    }

    #endregion

    #region LoadAllConfigsAsync Tests

    [Fact]
    public async Task LoadAllConfigsAsync_LoadsAllServiceTypes()
    {
        // Arrange - Save configs for all service types
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://metadata.com",
            "metadata-key",
            TestPassword);

        await _registry.SetConfigAsync(
            DataServiceType.TokenPrice,
            "https://price.com",
            "price-key",
            TestPassword);

        _sessionService.ClearCache();

        // Act
        await _registry.LoadAllConfigsAsync(TestPassword);

        // Assert - All configs should be loaded into session
        Assert.Equal("https://metadata.com", _sessionService.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Equal("metadata-key", _sessionService.GetDataServiceApiKey(DataServiceType.TokenMetadata));
        Assert.Equal("https://price.com", _sessionService.GetDataServiceEndpoint(DataServiceType.TokenPrice));
        Assert.Equal("price-key", _sessionService.GetDataServiceApiKey(DataServiceType.TokenPrice));
    }

    [Fact]
    public async Task LoadAllConfigsAsync_WithNoConfigs_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        await _registry.LoadAllConfigsAsync(TestPassword);
    }

    [Fact]
    public async Task LoadAllConfigsAsync_PartialConfigs_LoadsAvailable()
    {
        // Arrange - Only set one config
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://metadata.com",
            null,
            TestPassword);

        _sessionService.ClearCache();

        // Act
        await _registry.LoadAllConfigsAsync(TestPassword);

        // Assert
        Assert.Equal("https://metadata.com", _sessionService.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Null(_sessionService.GetDataServiceEndpoint(DataServiceType.TokenPrice));
    }

    #endregion

    #region GetAllConfigsAsync Tests

    [Fact]
    public async Task GetAllConfigsAsync_WhenEmpty_ReturnsEmptyList()
    {
        // Act
        IReadOnlyList<DataServiceConfig> configs = await _registry.GetAllConfigsAsync();

        // Assert
        Assert.Empty(configs);
    }

    [Fact]
    public async Task GetAllConfigsAsync_ReturnsAllConfigs()
    {
        // Arrange
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://metadata.com",
            null,
            TestPassword);

        await _registry.SetConfigAsync(
            DataServiceType.TokenPrice,
            "https://price.com",
            "price-key",
            TestPassword);

        // Act
        IReadOnlyList<DataServiceConfig> configs = await _registry.GetAllConfigsAsync();

        // Assert
        Assert.Equal(2, configs.Count);
        Assert.Contains(configs, c => c.ServiceType == DataServiceType.TokenMetadata);
        Assert.Contains(configs, c => c.ServiceType == DataServiceType.TokenPrice);
    }

    #endregion

    #region IDataServiceSessionProvider Tests

    [Fact]
    public async Task SessionService_ImplementsIDataServiceSessionProvider()
    {
        // Arrange
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://metadata.com",
            "metadata-key",
            TestPassword);

        // Act - Use session service as IDataServiceSessionProvider
        Buriza.Data.Services.IDataServiceSessionProvider provider = _sessionService;

        // Assert
        Assert.Equal("https://metadata.com", provider.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Equal("metadata-key", provider.GetDataServiceApiKey(DataServiceType.TokenMetadata));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task SetConfigAsync_WithSpecialCharactersInApiKey_Works()
    {
        // Arrange
        const string specialApiKey = "key!@#$%^&*()_+-=[]{}|;':\",./<>?";

        // Act
        await _registry.SetConfigAsync(
            DataServiceType.TokenPrice,
            "https://price.com",
            specialApiKey,
            TestPassword);

        // Assert
        _sessionService.ClearCache();
        await _registry.LoadConfigAsync(DataServiceType.TokenPrice, TestPassword);
        Assert.Equal(specialApiKey, _sessionService.GetDataServiceApiKey(DataServiceType.TokenPrice));
    }

    [Fact]
    public async Task SetConfigAsync_WithUnicodeApiKey_Works()
    {
        // Arrange
        const string unicodeApiKey = "密码🔐Пароль";

        // Act
        await _registry.SetConfigAsync(
            DataServiceType.TokenPrice,
            "https://price.com",
            unicodeApiKey,
            TestPassword);

        // Assert
        _sessionService.ClearCache();
        await _registry.LoadConfigAsync(DataServiceType.TokenPrice, TestPassword);
        Assert.Equal(unicodeApiKey, _sessionService.GetDataServiceApiKey(DataServiceType.TokenPrice));
    }

    [Fact]
    public async Task SetConfigAsync_WithLongEndpoint_Works()
    {
        // Arrange - Use a valid URL structure with long path
        string longEndpoint = "https://api.example.com/" + new string('a', 200) + "/v1/endpoint";

        // Act
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            longEndpoint,
            null,
            TestPassword);

        // Assert
        DataServiceConfig? config = await _registry.GetConfigAsync(DataServiceType.TokenMetadata);
        Assert.Equal(longEndpoint, config?.Endpoint);
    }

    [Fact]
    public async Task FullWorkflow_SetClearLoadCycle_Works()
    {
        // 1. Set config
        await _registry.SetConfigAsync(
            DataServiceType.TokenMetadata,
            "https://custom.com",
            "custom-key",
            TestPassword,
            "Custom Service");

        Assert.NotNull(await _registry.GetConfigAsync(DataServiceType.TokenMetadata));
        Assert.Equal("https://custom.com", _sessionService.GetDataServiceEndpoint(DataServiceType.TokenMetadata));

        // 2. Clear session (simulates app restart)
        _sessionService.ClearCache();
        Assert.Null(_sessionService.GetDataServiceEndpoint(DataServiceType.TokenMetadata));

        // 3. Load config back into session
        await _registry.LoadConfigAsync(DataServiceType.TokenMetadata, TestPassword);
        Assert.Equal("https://custom.com", _sessionService.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Equal("custom-key", _sessionService.GetDataServiceApiKey(DataServiceType.TokenMetadata));

        // 4. Clear config entirely
        await _registry.ClearConfigAsync(DataServiceType.TokenMetadata);
        Assert.Null(await _registry.GetConfigAsync(DataServiceType.TokenMetadata));
        Assert.Null(_sessionService.GetDataServiceEndpoint(DataServiceType.TokenMetadata));
        Assert.Null(_sessionService.GetDataServiceApiKey(DataServiceType.TokenMetadata));
    }

    #endregion
}
