using System.Text;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;
using Buriza.Core.Services;
using Buriza.Core.Storage;
using Buriza.Data.Models;
using Buriza.Data.Services;
using Buriza.Tests.Mocks;

namespace Buriza.Tests.Unit.Services;

public class BurizaStorageAuthMethodsTests : IDisposable
{
    private readonly TestWalletStorageService _storage;
    private readonly BurizaChainProviderFactory _providerFactory;
    private readonly WalletManagerService _walletManager;
    private readonly Guid _walletId = Guid.NewGuid();

    public BurizaStorageAuthMethodsTests()
    {
        InMemoryStorage platformStorage = new();
        _storage = new TestWalletStorageService(platformStorage);
        ChainProviderSettings settings = new()
        {
            Cardano = new CardanoSettings
            {
                MainnetEndpoint = "https://test-mainnet.example.com",
                PreprodEndpoint = "https://test-preprod.example.com",
                PreviewEndpoint = "https://test-preview.example.com"
            }
        };
        _providerFactory = new BurizaChainProviderFactory(new TestAppStateService(), settings);
        _walletManager = new WalletManagerService(_storage, _providerFactory);
    }

    public void Dispose()
    {
        _providerFactory.Dispose();
        GC.SuppressFinalize(this);
    }

    #region BurizaStorageBase Default Device Auth Tests

    [Fact]
    public async Task GetCapabilitiesAsync_Default_ReturnsNotSupported()
    {
        DeviceCapabilities capabilities = await _storage.GetCapabilitiesAsync();

        Assert.False(capabilities.IsSupported);
        Assert.False(capabilities.SupportsBiometrics);
        Assert.False(capabilities.SupportsPin);
        Assert.Empty(capabilities.AvailableTypes);
    }

    [Fact]
    public async Task GetEnabledAuthMethodsAsync_Default_ReturnsEmptySet()
    {
        HashSet<AuthenticationType> methods = await _storage.GetEnabledAuthMethodsAsync(_walletId);

        Assert.Empty(methods);
    }

    [Fact]
    public async Task IsDeviceAuthEnabledAsync_Default_ReturnsFalse()
    {
        bool enabled = await _storage.IsDeviceAuthEnabledAsync(_walletId);

        Assert.False(enabled);
    }

    [Fact]
    public async Task IsBiometricEnabledAsync_Default_ReturnsFalse()
    {
        bool enabled = await _storage.IsBiometricEnabledAsync(_walletId);

        Assert.False(enabled);
    }

    [Fact]
    public async Task IsPinEnabledAsync_Default_ReturnsFalse()
    {
        bool enabled = await _storage.IsPinEnabledAsync(_walletId);

        Assert.False(enabled);
    }

    [Fact]
    public async Task EnableAuthAsync_Default_ThrowsNotSupportedException()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _storage.EnableAuthAsync(_walletId, AuthenticationType.Biometric, Encoding.UTF8.GetBytes("password")));
    }

    [Fact]
    public async Task DisableAuthMethodAsync_Default_ThrowsNotSupported()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _storage.DisableAuthMethodAsync(_walletId, AuthenticationType.Biometric));
    }

    [Fact]
    public async Task DisableAllDeviceAuthAsync_Default_ThrowsNotSupported()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _storage.DisableAllDeviceAuthAsync(_walletId));
    }

    [Fact]
    public async Task AuthenticateWithDeviceAuthAsync_Default_ThrowsNotSupportedException()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _storage.AuthenticateWithDeviceAuthAsync(_walletId, AuthenticationType.Biometric, "Authenticate"));
    }

    #endregion

    #region WalletManagerService Auth Forwarding Tests

    [Fact]
    public async Task WalletManagerService_GetDeviceCapabilitiesAsync_DelegatesToStorage()
    {
        DeviceCapabilities capabilities = await _walletManager.GetDeviceCapabilitiesAsync();

        Assert.False(capabilities.IsSupported);
        Assert.False(capabilities.SupportsBiometrics);
        Assert.False(capabilities.SupportsPin);
        Assert.Empty(capabilities.AvailableTypes);
    }

    [Fact]
    public async Task WalletManagerService_GetEnabledAuthMethodsAsync_DelegatesToStorage()
    {
        HashSet<AuthenticationType> methods = await _walletManager.GetEnabledAuthMethodsAsync(_walletId);

        Assert.Empty(methods);
    }

    [Fact]
    public async Task WalletManagerService_IsDeviceAuthEnabledAsync_DelegatesToStorage()
    {
        bool enabled = await _walletManager.IsDeviceAuthEnabledAsync(_walletId);

        Assert.False(enabled);
    }

    [Fact]
    public async Task WalletManagerService_IsBiometricEnabledAsync_DelegatesToStorage()
    {
        bool enabled = await _walletManager.IsBiometricEnabledAsync(_walletId);

        Assert.False(enabled);
    }

    [Fact]
    public async Task WalletManagerService_IsPinEnabledAsync_DelegatesToStorage()
    {
        bool enabled = await _walletManager.IsPinEnabledAsync(_walletId);

        Assert.False(enabled);
    }

    [Fact]
    public async Task WalletManagerService_EnableAuthAsync_DelegatesToStorage()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _walletManager.EnableAuthAsync(_walletId, AuthenticationType.Biometric, Encoding.UTF8.GetBytes("password")));
    }

    [Fact]
    public async Task WalletManagerService_DisableAuthMethodAsync_DelegatesToStorage()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _walletManager.DisableAuthMethodAsync(_walletId, AuthenticationType.Biometric));
    }

    [Fact]
    public async Task WalletManagerService_DisableAllDeviceAuthAsync_DelegatesToStorage()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _walletManager.DisableAllDeviceAuthAsync(_walletId));
    }

    #endregion
}
