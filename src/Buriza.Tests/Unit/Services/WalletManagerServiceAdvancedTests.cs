using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Models.Chain;
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
/// Advanced tests for WalletManagerService covering:
/// - Wallet Lock/Unlock flows
/// - SetActiveChainAsync
/// - Password verification and change flows
/// - Edge cases and error scenarios
/// </summary>
public class WalletManagerServiceAdvancedTests : IDisposable
{
    private readonly InMemoryStorage _platformStorage;
    private readonly TestWalletStorageService _storage;
    private readonly BurizaChainProviderFactory _providerFactory;
    private readonly WalletManagerService _walletManager;

    private const string TestPassword = "TestPassword123!";
    private static readonly byte[] TestPasswordBytes = Encoding.UTF8.GetBytes(TestPassword);
    private const string TestMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    public WalletManagerServiceAdvancedTests()
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
        _providerFactory.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task<BurizaWallet> CreateWalletAsync(string name, string mnemonic, string password, ChainInfo? chainInfo = null)
        => _walletManager.CreateAsync(name, Encoding.UTF8.GetBytes(mnemonic), Encoding.UTF8.GetBytes(password), chainInfo);

    #region Wallet Lock/Unlock Tests

    [Fact]
    public async Task UnlockAsync_DerivesAddressOnDemand()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);
        Assert.False(wallet.IsUnlocked);

        // Act
        await wallet.UnlockAsync(TestPasswordBytes);

        // Assert
        Assert.True(wallet.IsUnlocked);
        ChainAddressData? data = await wallet.GetAddressInfoAsync();
        Assert.NotNull(data);
        Assert.False(string.IsNullOrEmpty(data.Address));
    }

    [Fact]
    public async Task Lock_ClearsMnemonicFromMemory()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);
        await wallet.UnlockAsync(TestPasswordBytes);
        Assert.True(wallet.IsUnlocked);

        // Act
        wallet.Lock();

        // Assert
        Assert.False(wallet.IsUnlocked);
        ChainAddressData? data = await wallet.GetAddressInfoAsync();
        Assert.Null(data);
    }

    [Fact]
    public async Task UnlockAsync_WhenAlreadyUnlocked_IsNoOp()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);
        await wallet.UnlockAsync(TestPasswordBytes);
        string? firstAddress = (await wallet.GetAddressInfoAsync())?.Address;

        // Act - Unlock again
        await wallet.UnlockAsync(TestPasswordBytes);
        string? secondAddress = (await wallet.GetAddressInfoAsync())?.Address;

        // Assert
        Assert.Equal(firstAddress, secondAddress);
    }

    #endregion

    #region SetActiveChainAsync Tests

    [Fact]
    public async Task SetActiveChainAsync_SwitchesChain()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act
        await wallet.SetActiveChainAsync(ChainRegistryData.CardanoMainnet);

        // Assert
        BurizaWallet? reloaded = await _walletManager.GetActiveAsync();
        Assert.Equal(ChainType.Cardano, reloaded?.ActiveChain);
    }

    [Fact]
    public async Task SetActiveChainAsync_UpdatesLastAccessedAt()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);
        DateTime? beforeLastAccess = wallet.LastAccessedAt;

        await Task.Delay(10); // Small delay to ensure timestamp difference

        // Act
        await wallet.SetActiveChainAsync(ChainRegistryData.CardanoMainnet);

        // Assert
        BurizaWallet? reloaded = await _walletManager.GetActiveAsync();
        Assert.NotNull(reloaded?.LastAccessedAt);
        if (beforeLastAccess.HasValue)
            Assert.True(reloaded.LastAccessedAt > beforeLastAccess);
    }

    [Fact]
    public async Task SetActiveChainAsync_WithUnsupportedChain_ThrowsNotSupportedException()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act & Assert - Using an unsupported chain type (cast to simulate future chain)
        ChainInfo unsupportedChain = new() { Chain = (ChainType)999, Network = "mainnet", Symbol = "UNKNOWN", Name = "Unknown", Decimals = 0 };
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            wallet.SetActiveChainAsync(unsupportedChain));
    }

    #endregion

    #region Password Verification Tests

    [Fact]
    public async Task VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act
        bool result = await wallet.VerifyPasswordAsync(TestPasswordBytes);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act
        bool result = await wallet.VerifyPasswordAsync(Encoding.UTF8.GetBytes("WrongPassword!"));

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Password Change Tests

    [Fact]
    public async Task ChangePassword_WithCorrectOldPassword_Succeeds()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);
        const string newPassword = "NewPassword456!";

        // Act
        await wallet.ChangePasswordAsync(TestPasswordBytes, Encoding.UTF8.GetBytes(newPassword));

        // Assert - Old password should no longer work
        Assert.False(await wallet.VerifyPasswordAsync(TestPasswordBytes));
        Assert.True(await wallet.VerifyPasswordAsync(Encoding.UTF8.GetBytes(newPassword)));
    }

    [Fact]
    public async Task ChangePassword_PreservesMnemonic()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);
        const string newPassword = "NewPassword456!";

        // Act
        await wallet.ChangePasswordAsync(TestPasswordBytes, Encoding.UTF8.GetBytes(newPassword));

        // Assert - Should be able to decrypt and get same mnemonic
        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(wallet.Id, Encoding.UTF8.GetBytes(newPassword));
        string decryptedMnemonic = Encoding.UTF8.GetString(mnemonicBytes);
        Assert.Equal(TestMnemonic, decryptedMnemonic);

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes);
    }

    [Fact]
    public async Task ChangePassword_WithWrongOldPassword_ThrowsCryptographicException()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(() =>
            wallet.ChangePasswordAsync(Encoding.UTF8.GetBytes("WrongPassword!"), Encoding.UTF8.GetBytes("NewPassword456!")));
    }

    #endregion

    #region ExportMnemonic Advanced Tests

    [Fact]
    public async Task ExportMnemonic_ReturnsCorrectLength()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);
        await wallet.UnlockAsync(TestPasswordBytes);
        int? exportedLength = null;

        // Act
        wallet.ExportMnemonic(mnemonic =>
        {
            exportedLength = mnemonic.Length;
        });

        // Assert
        Assert.Equal(TestMnemonic.Length, exportedLength);
    }

    [Fact]
    public async Task ExportMnemonic_CallbackReceivesCorrectMnemonic()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);
        await wallet.UnlockAsync(TestPasswordBytes);
        string? exportedMnemonic = null;

        // Act
        wallet.ExportMnemonic(mnemonic =>
        {
            exportedMnemonic = mnemonic.ToString();
        });

        // Assert
        Assert.Equal(TestMnemonic, exportedMnemonic);
    }

    #endregion

    #region Multiple Account Tests

    [Fact]
    public async Task CreateMultipleAccounts_EachHasUniqueAddresses()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);
        await wallet.UnlockAsync(TestPasswordBytes);
        string? account0Address = (await wallet.GetAddressInfoAsync(0))?.Address;

        // Act - Create second account
        await wallet.CreateAccountAsync("Account 2");

        // Reload to get updated accounts, unlock, and derive account 1 address
        BurizaWallet? reloaded = await _walletManager.GetActiveAsync();
        Assert.NotNull(reloaded);
        await reloaded.UnlockAsync(TestPasswordBytes);
        string? account1Address = (await reloaded.GetAddressInfoAsync(1))?.Address;

        // Assert - Addresses should be different
        Assert.NotNull(account0Address);
        Assert.NotNull(account1Address);
        Assert.NotEqual(account0Address, account1Address);
    }

    [Fact]
    public async Task GetActiveAccount_ReturnsCorrectAccount()
    {
        // Arrange
        BurizaWallet wallet = await CreateWalletAsync("Test Wallet", TestMnemonic, TestPassword);
        await wallet.CreateAccountAsync("Account 2");
        await wallet.SetActiveAccountAsync(1);

        // Act
        BurizaWallet? reloaded = await _walletManager.GetActiveAsync();
        BurizaWalletAccount? activeAccount = reloaded?.GetActiveAccount();

        // Assert
        Assert.NotNull(activeAccount);
        Assert.Equal(1, activeAccount.Index);
        Assert.Equal("Account 2", activeAccount.Name);
    }

    [Fact]
    public async Task GetActiveAccount_WhenNoWallet_ReturnsNull()
    {
        // Act
        BurizaWallet? wallet = await _walletManager.GetActiveAsync();
        BurizaWalletAccount? activeAccount = wallet?.GetActiveAccount();

        // Assert
        Assert.Null(activeAccount);
    }

    #endregion

    #region Wallet Lifecycle Edge Cases

    [Fact]
    public async Task DeleteActiveWallet_WithMultipleWallets_SwitchesToRemaining()
    {
        // Arrange
        BurizaWallet wallet1 = await CreateWalletAsync("Wallet 1", TestMnemonic, TestPassword);
        BurizaWallet wallet2 = await CreateWalletAsync("Wallet 2", TestMnemonic, TestPassword);

        // wallet2 should be active (most recently created)
        BurizaWallet? active = await _walletManager.GetActiveAsync();
        Assert.Equal(wallet2.Id, active?.Id);

        // Act - Delete the active wallet
        await _walletManager.DeleteAsync(wallet2.Id);

        // Assert - Should switch to wallet1
        active = await _walletManager.GetActiveAsync();
        Assert.NotNull(active);
        Assert.Equal(wallet1.Id, active.Id);
    }

    [Fact]
    public async Task CreateAsync_WithDifferentNetworks_DerivesCorrectAddresses()
    {
        // Act
        BurizaWallet mainnetWallet = await CreateWalletAsync(
            "Mainnet Wallet", TestMnemonic, TestPassword, ChainRegistryData.CardanoMainnet);
        BurizaWallet preprodWallet = await CreateWalletAsync(
            "Preprod Wallet", TestMnemonic, TestPassword, ChainRegistryData.CardanoPreprod);

        await mainnetWallet.UnlockAsync(TestPasswordBytes);
        await preprodWallet.UnlockAsync(TestPasswordBytes);

        // Assert - Different networks should have different address prefixes
        string? mainnetAddr = (await mainnetWallet.GetAddressInfoAsync())?.Address;
        string? preprodAddr = (await preprodWallet.GetAddressInfoAsync())?.Address;

        Assert.NotNull(mainnetAddr);
        Assert.NotNull(preprodAddr);
        Assert.StartsWith("addr1", mainnetAddr);
        Assert.StartsWith("addr_test1", preprodAddr);
        Assert.NotEqual(mainnetAddr, preprodAddr);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsWalletsWithAttachedProviders()
    {
        // Arrange
        await CreateWalletAsync("Wallet 1", TestMnemonic, TestPassword);
        await CreateWalletAsync("Wallet 2", TestMnemonic, TestPassword);

        // Act
        IReadOnlyList<BurizaWallet> wallets = await _walletManager.GetAllAsync();

        // Assert - All wallets should be able to derive address when unlocked
        Assert.Equal(2, wallets.Count);
        foreach (BurizaWallet wallet in wallets)
        {
            await wallet.UnlockAsync(TestPasswordBytes);
            Assert.NotNull((await wallet.GetAddressInfoAsync())?.Address);
        }
    }

    #endregion
}
