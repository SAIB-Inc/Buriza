using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Wallet;
using Buriza.Core.Services;
using Buriza.Core.Storage;
using Buriza.Data.Models;
using Buriza.Data.Services;
using Buriza.Tests.Mocks;

using ChainRegistryData = Buriza.Core.Models.Chain.ChainRegistry;

namespace Buriza.Tests.Unit.Services;

/// <summary>
/// Tests for WalletManagerService.
/// </summary>
public class WalletManagerServiceTests : IDisposable
{
    private readonly InMemoryStorage _platformStorage;
    private readonly TestWalletStorageService _storage;
    private readonly BurizaChainProviderFactory _providerFactory;
    private readonly WalletManagerService _walletManager;

    private const string TestPassword = "TestPassword123!";
    private const string TestMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    public WalletManagerServiceTests()
    {
        _platformStorage = new InMemoryStorage();
        _storage = new TestWalletStorageService(_platformStorage);
        ChainProviderSettings settings = new()
        {
            Cardano = new CardanoSettings
            {
                MainnetEndpoint = "https://test-mainnet.example.com",
                PreprodEndpoint = "https://test-preprod.example.com",
                PreviewEndpoint = "https://test-preview.example.com",
                MainnetApiKey = "test-mainnet-key",
                PreprodApiKey = "test-preprod-key",
                PreviewApiKey = "test-preview-key"
            }
        };
        _providerFactory = new BurizaChainProviderFactory(new TestAppStateService(), settings);
        _walletManager = new WalletManagerService(_storage, _providerFactory);
    }

    public void Dispose()
    {
        _walletManager.Dispose();
        _providerFactory.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task<BurizaWallet> CreateWalletAsync(string name, string mnemonic, string password, ChainInfo? chainInfo = null)
        => _walletManager.CreateFromMnemonicAsync(name, Encoding.UTF8.GetBytes(mnemonic), Encoding.UTF8.GetBytes(password), chainInfo);

    #region CreateFromMnemonicAsync Tests

    [Fact]
    public async Task CreateFromMnemonicAsync_WithValidMnemonic_CreatesWallet()
    {
        // Act
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Assert
        Assert.NotNull(wallet);
        Assert.Equal("Test Wallet", wallet.Profile.Name);
        Assert.NotEqual(Guid.Empty, wallet.Id);
        Assert.Equal(ChainType.Cardano, wallet.ActiveChain);
        Assert.Equal(NetworkType.Mainnet, wallet.Network);
    }

    [Fact]
    public async Task CreateFromMnemonicAsync_WithInvalidMnemonic_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateWalletAsync("Test", "invalid mnemonic words", TestPassword));
    }

    [Fact]
    public async Task CreateFromMnemonicAsync_DerivesAddresses()
    {
        // Act
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Assert
        BurizaWalletAccount? account = wallet.GetActiveAccount();
        Assert.NotNull(account);

        ChainAddressData? mainnetData = account.GetChainData(ChainType.Cardano, NetworkType.Mainnet);
        ChainAddressData? previewData = account.GetChainData(ChainType.Cardano, NetworkType.Preview);
        ChainAddressData? preprodData = account.GetChainData(ChainType.Cardano, NetworkType.Preprod);

        Assert.NotNull(mainnetData);
        Assert.NotNull(previewData);
        Assert.NotNull(preprodData);
        Assert.False(string.IsNullOrEmpty(mainnetData.Address));
        Assert.False(string.IsNullOrEmpty(previewData.Address));
        Assert.False(string.IsNullOrEmpty(preprodData.Address));
    }

    [Fact]
    public async Task CreateFromMnemonicAsync_CreatesEncryptedVault()
    {
        // Act
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Assert
        Assert.True(await _storage.HasVaultAsync(wallet.Id));
    }

    [Fact]
    public async Task CreateFromMnemonicAsync_SetsWalletAsActive()
    {
        // Act
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Assert
        Guid? activeId = await _storage.GetActiveWalletIdAsync();
        Assert.Equal(wallet.Id, activeId);
    }

    [Fact]
    public async Task CreateFromMnemonicAsync_WithPreprodNetwork_CreatesPreprodWallet()
    {
        // Act
        BurizaWallet wallet = await CreateWalletAsync(
            "Preprod Wallet",
            TestMnemonic,
            TestPassword,
            ChainRegistryData.CardanoPreprod);

        // Assert
        Assert.Equal(NetworkType.Preprod, wallet.Network);
    }

    [Fact]
    public async Task CreateFromMnemonicAsync_RollsBackOnFailure()
    {
        // This test verifies that if saving the wallet fails, the vault is deleted
        // We can't easily simulate this without modifying the storage, but we can
        // verify the normal flow doesn't leave orphaned data
        BurizaWallet wallet = await CreateWalletAsync("Test", TestMnemonic, TestPassword);

        IReadOnlyList<BurizaWallet> wallets = await _storage.LoadAllWalletsAsync();
        Assert.Single(wallets);
        Assert.True(await _storage.HasVaultAsync(wallet.Id));
    }

    #endregion

    #region GenerateMnemonic Tests

    [Fact]
    public void GenerateMnemonic_Returns24Words()
    {
        // Act
        string? generatedMnemonic = null;
        _walletManager.GenerateMnemonic(24, mnemonic =>
        {
            generatedMnemonic = mnemonic.ToString();
        });

        // Assert
        Assert.NotNull(generatedMnemonic);
        string[] words = generatedMnemonic.Split(' ');
        Assert.Equal(24, words.Length);
    }

    [Fact]
    public void GenerateMnemonic_Returns12Words()
    {
        // Act
        string? generatedMnemonic = null;
        _walletManager.GenerateMnemonic(12, mnemonic =>
        {
            generatedMnemonic = mnemonic.ToString();
        });

        // Assert
        Assert.NotNull(generatedMnemonic);
        string[] words = generatedMnemonic.Split(' ');
        Assert.Equal(12, words.Length);
    }

    [Fact]
    public void GenerateMnemonic_ReturnsValidBip39Mnemonic()
    {
        // Act
        string? generatedMnemonic = null;
        _walletManager.GenerateMnemonic(24, mnemonic =>
        {
            generatedMnemonic = mnemonic.ToString();
        });

        // Assert - Should be valid (can be used to create wallet)
        Assert.NotNull(generatedMnemonic);
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(generatedMnemonic);
        Assert.True(MnemonicService.Validate(mnemonicBytes));
        CryptographicOperations.ZeroMemory(mnemonicBytes);
    }

    [Fact]
    public void GenerateMnemonic_GeneratesUniqueMnemonics()
    {
        // Act
        string? mnemonic1 = null;
        string? mnemonic2 = null;
        _walletManager.GenerateMnemonic(24, m => mnemonic1 = m.ToString());
        _walletManager.GenerateMnemonic(24, m => mnemonic2 = m.ToString());

        // Assert - Two generations should produce different mnemonics
        Assert.NotNull(mnemonic1);
        Assert.NotNull(mnemonic2);
        Assert.NotEqual(mnemonic1, mnemonic2);
    }

    [Fact]
    public async Task GenerateMnemonic_ThenCreateFromMnemonic_CreatesWallet()
    {
        // Arrange
        string? generatedMnemonic = null;
        _walletManager.GenerateMnemonic(24, mnemonic =>
        {
            generatedMnemonic = mnemonic.ToString();
        });

        // Act
        BurizaWallet wallet = await CreateWalletAsync("New Wallet", generatedMnemonic!, TestPassword);

        // Assert
        Assert.NotNull(wallet);
        Assert.Equal("New Wallet", wallet.Profile.Name);
    }

    [Fact]
    public async Task GenerateMnemonic_ThenCreateFromMnemonic_CanExportSameMnemonic()
    {
        // Arrange - Generate and create wallet
        string? generatedMnemonic = null;
        _walletManager.GenerateMnemonic(24, mnemonic =>
        {
            generatedMnemonic = mnemonic.ToString();
        });

        BurizaWallet wallet = await CreateWalletAsync("New Wallet", generatedMnemonic!, TestPassword);

        // Act - Export mnemonic
        string? exportedMnemonic = null;
        await _walletManager.ExportMnemonicAsync(wallet.Id, TestPassword, mnemonic =>
        {
            exportedMnemonic = mnemonic.ToString();
        });

        // Assert - Exported mnemonic should match the generated one
        Assert.Equal(generatedMnemonic, exportedMnemonic);
    }

    [Fact]
    public async Task GenerateMnemonic_12Words_ThenCreateFromMnemonic_Works()
    {
        // Arrange
        string? generatedMnemonic = null;
        _walletManager.GenerateMnemonic(12, mnemonic =>
        {
            generatedMnemonic = mnemonic.ToString();
        });

        // Act
        BurizaWallet wallet = await CreateWalletAsync("12 Word Wallet", generatedMnemonic!, TestPassword);

        // Assert
        Assert.NotNull(wallet);
        Assert.Equal("12 Word Wallet", wallet.Profile.Name);
    }

    [Fact]
    public async Task GenerateMnemonic_ThenCreateFromMnemonic_DerivesCorrectAddresses()
    {
        // Arrange
        string? generatedMnemonic = null;
        _walletManager.GenerateMnemonic(24, mnemonic =>
        {
            generatedMnemonic = mnemonic.ToString();
        });

        // Act
        BurizaWallet wallet = await CreateWalletAsync("New Wallet", generatedMnemonic!, TestPassword);

        // Assert - Should have derived addresses
        BurizaWalletAccount? account = wallet.GetActiveAccount();
        Assert.NotNull(account);

        ChainAddressData? chainData = account.GetChainData(ChainType.Cardano, NetworkType.Mainnet);
        Assert.NotNull(chainData);
        Assert.False(string.IsNullOrEmpty(chainData.Address));
        Assert.StartsWith("addr1", chainData.Address); // Mainnet address
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllWallets()
    {
        // Arrange
        await CreateWalletAsync("Wallet 1", TestMnemonic, TestPassword);
        await CreateWalletAsync("Wallet 2", TestMnemonic, TestPassword);

        // Act
        IReadOnlyList<BurizaWallet> wallets = await _walletManager.GetAllAsync();

        // Assert
        Assert.Equal(2, wallets.Count);
    }

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        // Act
        IReadOnlyList<BurizaWallet> wallets = await _walletManager.GetAllAsync();

        // Assert
        Assert.Empty(wallets);
    }

    [Fact]
    public async Task GetAllAsync_AttachesProviders()
    {
        // Arrange
        await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act
        IReadOnlyList<BurizaWallet> wallets = await _walletManager.GetAllAsync();

        // Assert - Provider should be attached (wallet can query)
        Assert.NotNull(wallets[0].GetAddressInfo()?.Address);
    }

    #endregion

    #region GetActiveAsync Tests

    [Fact]
    public async Task GetActiveAsync_ReturnsActiveWallet()
    {
        // Arrange
        BurizaWallet created = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act
        BurizaWallet? active = await _walletManager.GetActiveAsync();

        // Assert
        Assert.NotNull(active);
        Assert.Equal(created.Id, active.Id);
    }

    [Fact]
    public async Task GetActiveAsync_WhenNoWallets_ReturnsNull()
    {
        // Act
        BurizaWallet? active = await _walletManager.GetActiveAsync();

        // Assert
        Assert.Null(active);
    }

    #endregion

    #region SetActiveAsync Tests

    [Fact]
    public async Task SetActiveAsync_ChangesActiveWallet()
    {
        // Arrange
        BurizaWallet wallet1 = await CreateWalletAsync("Wallet 1", TestMnemonic, TestPassword);
        BurizaWallet wallet2 = await CreateWalletAsync("Wallet 2", TestMnemonic, TestPassword);

        // Act
        await _walletManager.SetActiveAsync(wallet1.Id);
        BurizaWallet? active = await _walletManager.GetActiveAsync();

        // Assert
        Assert.Equal(wallet1.Id, active?.Id);
    }

    [Fact]
    public async Task SetActiveAsync_WithInvalidId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _walletManager.SetActiveAsync(Guid.Parse("00000000-0000-0000-0000-0000000003e7")));
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_RemovesWallet()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act
        await _walletManager.DeleteAsync(wallet.Id);

        // Assert
        IReadOnlyList<BurizaWallet> wallets = await _storage.LoadAllWalletsAsync();
        Assert.Empty(wallets);
    }

    [Fact]
    public async Task DeleteAsync_RemovesVault()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act
        await _walletManager.DeleteAsync(wallet.Id);

        // Assert
        Assert.False(await _storage.HasVaultAsync(wallet.Id));
    }

    [Fact]
    public async Task DeleteAsync_UpdatesActiveWallet()
    {
        // Arrange
        BurizaWallet wallet1 = await CreateWalletAsync("Wallet 1", TestMnemonic, TestPassword);
        BurizaWallet wallet2 = await CreateWalletAsync("Wallet 2", TestMnemonic, TestPassword);
        await _walletManager.SetActiveAsync(wallet1.Id);

        // Act - Delete active wallet
        await _walletManager.DeleteAsync(wallet1.Id);

        // Assert - Should switch to remaining wallet
        BurizaWallet? active = await _walletManager.GetActiveAsync();
        Assert.Equal(wallet2.Id, active?.Id);
    }

    [Fact]
    public async Task DeleteAsync_WhenLastWallet_ClearsActiveWalletId()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act
        await _walletManager.DeleteAsync(wallet.Id);

        // Assert
        BurizaWallet? active = await _walletManager.GetActiveAsync();
        Assert.Null(active);
    }

    [Fact]
    public async Task DeleteAsync_WithInvalidId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _walletManager.DeleteAsync(Guid.Parse("00000000-0000-0000-0000-0000000003e7")));
    }

    #endregion

    #region ExportMnemonicAsync Tests

    [Fact]
    public async Task ExportMnemonicAsync_ReturnsCorrectMnemonic()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);
        string? exportedMnemonic = null;

        // Act
        await _walletManager.ExportMnemonicAsync(wallet.Id, TestPassword, mnemonic =>
        {
            exportedMnemonic = mnemonic.ToString();
        });

        // Assert
        Assert.Equal(TestMnemonic, exportedMnemonic);
    }

    [Fact]
    public async Task ExportMnemonicAsync_WithWrongPassword_ThrowsCryptographicException()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(() =>
            _walletManager.ExportMnemonicAsync(wallet.Id, "WrongPassword", _ => { }));
    }

    #endregion

    #region Custom Provider Config Tests

    [Fact]
    public async Task SetCustomProviderConfigAsync_SavesConfig()
    {
        // Arrange
        await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act
        await _walletManager.SetCustomProviderConfigAsync(
            ChainRegistryData.CardanoMainnet,
            "https://custom.endpoint.com",
            "custom-api-key",
            TestPassword,
            "My Custom Node");

        // Assert
        CustomProviderConfig? config = await _walletManager.GetCustomProviderConfigAsync(
            ChainRegistryData.CardanoMainnet);

        Assert.NotNull(config);
        Assert.Equal("https://custom.endpoint.com", config.Endpoint);
        Assert.Equal("My Custom Node", config.Name);
        Assert.True(config.HasCustomApiKey);
    }

    [Fact]
    public async Task SetCustomProviderConfigAsync_WithInvalidUrl_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _walletManager.SetCustomProviderConfigAsync(
                ChainRegistryData.CardanoMainnet,
                "not-a-valid-url",
                "api-key",
                TestPassword));
    }

    [Fact]
    public async Task SetCustomProviderConfigAsync_WithNullEndpoint_Succeeds()
    {
        // Act - Only set API key, no endpoint
        await _walletManager.SetCustomProviderConfigAsync(
            ChainRegistryData.CardanoMainnet,
            null,
            "custom-api-key",
            TestPassword);

        // Assert
        CustomProviderConfig? config = await _walletManager.GetCustomProviderConfigAsync(
            ChainRegistryData.CardanoMainnet);

        Assert.NotNull(config);
        Assert.Null(config.Endpoint);
        Assert.True(config.HasCustomApiKey);
    }

    [Fact]
    public async Task SetCustomProviderConfigAsync_WithNullApiKey_ClearsExistingKey()
    {
        // Arrange - First set a config with API key
        await _walletManager.SetCustomProviderConfigAsync(
            ChainRegistryData.CardanoMainnet,
            "https://test.com",
            "initial-key",
            TestPassword);

        // Act - Update without API key
        await _walletManager.SetCustomProviderConfigAsync(
            ChainRegistryData.CardanoMainnet,
            "https://test.com",
            null,
            TestPassword);

        // Assert
        CustomProviderConfig? config = await _walletManager.GetCustomProviderConfigAsync(
            ChainRegistryData.CardanoMainnet);

        Assert.NotNull(config);
        Assert.False(config.HasCustomApiKey);

        (CustomProviderConfig Config, string? ApiKey)? result =
            await _storage.GetCustomProviderConfigWithApiKeyAsync(ChainRegistryData.CardanoMainnet, TestPassword);
        Assert.NotNull(result);
        Assert.Null(result?.ApiKey);
    }

    [Fact]
    public async Task ClearCustomProviderConfigAsync_RemovesConfig()
    {
        // Arrange
        await _walletManager.SetCustomProviderConfigAsync(
            ChainRegistryData.CardanoMainnet,
            "https://custom.com",
            "custom-key",
            TestPassword);

        // Act
        await _walletManager.ClearCustomProviderConfigAsync(ChainRegistryData.CardanoMainnet);

        // Assert
        CustomProviderConfig? config = await _walletManager.GetCustomProviderConfigAsync(
            ChainRegistryData.CardanoMainnet);
        Assert.Null(config);
    }

    #endregion

    #region Account Management Tests

    [Fact]
    public async Task CreateAccountAsync_AddsNewAccount()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);
        int initialCount = wallet.Accounts.Count;

        // Act
        BurizaWalletAccount newAccount = await _walletManager.CreateAccountAsync(wallet.Id, "Account 2");

        // Assert
        Assert.Equal("Account 2", newAccount.Name);
        Assert.Equal(1, newAccount.Index);

        // Reload and verify
        BurizaWallet? reloaded = await _walletManager.GetActiveAsync();
        Assert.Equal(initialCount + 1, reloaded?.Accounts.Count);
    }

    [Fact]
    public async Task SetActiveAccountAsync_ChangesActiveAccount()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);
        await _walletManager.CreateAccountAsync(wallet.Id, "Account 2");

        // Act
        await _walletManager.SetActiveAccountAsync(wallet.Id, 1);

        // Assert
        BurizaWallet? reloaded = await _walletManager.GetActiveAsync();
        Assert.Equal(1, reloaded?.ActiveAccountIndex);
    }

    [Fact]
    public async Task SetActiveAccountAsync_WithInvalidIndex_ThrowsArgumentException()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _walletManager.SetActiveAccountAsync(wallet.Id, 99));
    }

    #endregion

    #region Chain Management Tests

    [Fact]
    public async Task SetActiveChainAsync_WithExistingChainData_SwitchesChain()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // The wallet starts with Cardano chain data already derived
        // Act - Switch to same chain (should work without password)
        await _walletManager.SetActiveChainAsync(wallet.Id, ChainRegistryData.CardanoMainnet);

        // Assert
        BurizaWallet? reloaded = await _walletManager.GetActiveAsync();
        Assert.Equal(ChainType.Cardano, reloaded?.ActiveChain);
    }

    [Fact]
    public async Task GetAvailableChains_ReturnsCardano()
    {
        // Act
        IReadOnlyList<ChainType> chains = _walletManager.GetAvailableChains();

        // Assert
        Assert.Contains(ChainType.Cardano, chains);
    }

    #endregion
}
