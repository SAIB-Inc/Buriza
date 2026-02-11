using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Wallet;
using Buriza.Core.Services;
using Buriza.Data.Models;
using Buriza.Data.Services;
using Buriza.Tests.Mocks;

using ChainRegistryData = Buriza.Core.Models.Chain.ChainRegistry;

namespace Buriza.Tests.Unit.Services;

/// <summary>
/// Advanced tests for WalletManagerService covering:
/// - UnlockAccountAsync flows
/// - SetActiveChainAsync with password requirements
/// - Password verification and change flows
/// - Edge cases and error scenarios
/// </summary>
public class WalletManagerServiceAdvancedTests : IDisposable
{
    private readonly InMemoryPlatformStorage _platformStorage;
    private readonly TestWalletStorageService _storage;
    private readonly BurizaChainProviderFactory _providerFactory;
    private readonly WalletManagerService _walletManager;

    private const string TestPassword = "TestPassword123!";
    private const string TestMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    public WalletManagerServiceAdvancedTests()
    {
        _platformStorage = new InMemoryPlatformStorage();
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

    #region UnlockAccountAsync Tests

    [Fact]
    public async Task UnlockAccountAsync_WithExistingChainData_DoesNotChangeAddress()
    {
        // Arrange - Create wallet with default account
        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync("Test Wallet", TestMnemonic, TestPassword);
        BurizaWalletAccount? account = wallet.GetActiveAccount();
        Assert.NotNull(account);
        string? originalAddress = account.GetChainData(ChainType.Cardano, NetworkType.Mainnet)?.ReceiveAddress;
        Assert.False(string.IsNullOrEmpty(originalAddress));

        // Act - Unlock the account (should no-op because chain data exists)
        await _walletManager.UnlockAccountAsync(wallet.Id, 0, TestPassword);

        // Assert - Address remains the same
        BurizaWallet? reloaded = await _storage.LoadWalletAsync(wallet.Id);
        BurizaWalletAccount? reloadedAccount = reloaded?.GetActiveAccount();
        string? reloadedAddress = reloadedAccount?.GetChainData(ChainType.Cardano, NetworkType.Mainnet)?.ReceiveAddress;
        Assert.Equal(originalAddress, reloadedAddress);
    }

    [Fact]
    public async Task UnlockAccountAsync_WithInvalidWalletId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _walletManager.UnlockAccountAsync(Guid.Parse("00000000-0000-0000-0000-0000000003e7"), 0, TestPassword));
    }

    [Fact]
    public async Task UnlockAccountAsync_WithInvalidAccountIndex_ThrowsArgumentException()
    {
        // Arrange
        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _walletManager.UnlockAccountAsync(wallet.Id, 99, TestPassword));
    }

    [Fact]
    public async Task UnlockAccountAsync_WithNewAccount_DerivesAddresses()
    {
        // Arrange - Create wallet and add a new account
        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync("Test Wallet", TestMnemonic, TestPassword);
        BurizaWalletAccount newAccount = await _walletManager.CreateAccountAsync(wallet.Id, "Account 2");

        BurizaWallet? beforeUnlock = await _storage.LoadWalletAsync(wallet.Id);
        BurizaWalletAccount? beforeAccount = beforeUnlock?.Accounts.FirstOrDefault(a => a.Index == newAccount.Index);
        Assert.NotNull(beforeAccount);
        Assert.Null(beforeAccount.GetChainData(ChainType.Cardano, NetworkType.Mainnet));

        // Act - Unlock the new account (should derive addresses since no chain data exists)
        await _walletManager.UnlockAccountAsync(wallet.Id, newAccount.Index, TestPassword);

        // Assert - Chain data should now exist
        BurizaWallet? afterUnlock = await _storage.LoadWalletAsync(wallet.Id);
        BurizaWalletAccount? afterAccount = afterUnlock?.Accounts.FirstOrDefault(a => a.Index == newAccount.Index);
        Assert.NotNull(afterAccount);
        string? receiveAddress = afterAccount.GetChainData(ChainType.Cardano, NetworkType.Mainnet)?.ReceiveAddress;
        Assert.False(string.IsNullOrEmpty(receiveAddress));
    }

    #endregion

    #region SetActiveChainAsync Tests

    [Fact]
    public async Task SetActiveChainAsync_WithExistingChainData_NoPasswordRequired()
    {
        // Arrange
        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act - Switch to same chain (no password needed since data exists)
        await _walletManager.SetActiveChainAsync(wallet.Id, ChainRegistryData.CardanoMainnet);

        // Assert
        BurizaWallet? reloaded = await _walletManager.GetActiveAsync();
        Assert.Equal(ChainType.Cardano, reloaded?.ActiveChain);
    }

    [Fact]
    public async Task SetActiveChainAsync_UpdatesLastAccessedAt()
    {
        // Arrange
        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync("Test Wallet", TestMnemonic, TestPassword);
        DateTime? beforeLastAccess = wallet.LastAccessedAt;

        await Task.Delay(10); // Small delay to ensure timestamp difference

        // Act
        await _walletManager.SetActiveChainAsync(wallet.Id, ChainRegistryData.CardanoMainnet);

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
        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act & Assert - Using an unsupported chain type (cast to simulate future chain)
        ChainInfo unsupportedChain = new() { Chain = (ChainType)999, Network = NetworkType.Mainnet, Symbol = "UNKNOWN", Name = "Unknown", Decimals = 0 };
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _walletManager.SetActiveChainAsync(wallet.Id, unsupportedChain));
    }

    [Fact]
    public async Task SetActiveChainAsync_WithInvalidWalletId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _walletManager.SetActiveChainAsync(Guid.Parse("00000000-0000-0000-0000-0000000003e7"), ChainRegistryData.CardanoMainnet));
    }

    #endregion

    #region Password Verification Tests

    [Fact]
    public async Task VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act
        bool result = await _storage.VerifyPasswordAsync(wallet.Id, TestPassword);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        // Arrange
        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act
        bool result = await _storage.VerifyPasswordAsync(wallet.Id, "WrongPassword!");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyPassword_WithNonExistentWallet_ReturnsFalse()
    {
        // Act
        bool result = await _storage.VerifyPasswordAsync(Guid.Parse("00000000-0000-0000-0000-0000000003e7"), TestPassword);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Password Change Tests

    [Fact]
    public async Task ChangePassword_WithCorrectOldPassword_Succeeds()
    {
        // Arrange
        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync("Test Wallet", TestMnemonic, TestPassword);
        const string newPassword = "NewPassword456!";

        // Act
        await _storage.ChangePasswordAsync(wallet.Id, TestPassword, newPassword);

        // Assert - Old password should no longer work
        Assert.False(await _storage.VerifyPasswordAsync(wallet.Id, TestPassword));
        Assert.True(await _storage.VerifyPasswordAsync(wallet.Id, newPassword));
    }

    [Fact]
    public async Task ChangePassword_PreservesMnemonic()
    {
        // Arrange
        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync("Test Wallet", TestMnemonic, TestPassword);
        const string newPassword = "NewPassword456!";

        // Act
        await _storage.ChangePasswordAsync(wallet.Id, TestPassword, newPassword);

        // Assert - Should be able to decrypt and get same mnemonic
        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(wallet.Id, newPassword);
        string decryptedMnemonic = Encoding.UTF8.GetString(mnemonicBytes);
        Assert.Equal(TestMnemonic, decryptedMnemonic);

        // Cleanup
        CryptographicOperations.ZeroMemory(mnemonicBytes);
    }

    [Fact]
    public async Task ChangePassword_WithWrongOldPassword_ThrowsCryptographicException()
    {
        // Arrange
        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync("Test Wallet", TestMnemonic, TestPassword);

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(() =>
            _storage.ChangePasswordAsync(wallet.Id, "WrongPassword!", "NewPassword456!"));
    }

    #endregion

    #region ExportMnemonicAsync Advanced Tests

    [Fact]
    public async Task ExportMnemonicAsync_ReturnsCorrectLength()
    {
        // Arrange
        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync("Test Wallet", TestMnemonic, TestPassword);
        int? exportedLength = null;

        // Act
        await _walletManager.ExportMnemonicAsync(wallet.Id, TestPassword, mnemonic =>
        {
            exportedLength = mnemonic.Length;
        });

        // Assert
        Assert.Equal(TestMnemonic.Length, exportedLength);
    }

    [Fact]
    public async Task ExportMnemonicAsync_WithInvalidWalletId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _walletManager.ExportMnemonicAsync(Guid.Parse("00000000-0000-0000-0000-0000000003e7"), TestPassword, _ => { }));
    }

    [Fact]
    public async Task ExportMnemonicAsync_CallbackReceivesCorrectMnemonic()
    {
        // Arrange
        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync("Test Wallet", TestMnemonic, TestPassword);
        string? exportedMnemonic = null;

        // Act
        await _walletManager.ExportMnemonicAsync(wallet.Id, TestPassword, mnemonic =>
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
        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync("Test Wallet", TestMnemonic, TestPassword);
        string? account0Address = wallet.GetActiveAccount()
            ?.GetChainData(ChainType.Cardano, NetworkType.Mainnet)
            ?.ReceiveAddress;

        // Act - Create second account and unlock it
        BurizaWalletAccount account1 = await _walletManager.CreateAccountAsync(wallet.Id, "Account 2");
        await _walletManager.UnlockAccountAsync(wallet.Id, account1.Index, TestPassword);

        // Assert - Addresses should be different
        BurizaWallet? reloaded = await _storage.LoadWalletAsync(wallet.Id);
        string? account1Address = reloaded?.Accounts
            .FirstOrDefault(a => a.Index == account1.Index)
            ?.GetChainData(ChainType.Cardano, NetworkType.Mainnet)
            ?.ReceiveAddress;
        Assert.NotEqual(account0Address, account1Address);
        Assert.NotNull(account1Address);
    }

    [Fact]
    public async Task GetActiveAccountAsync_ReturnsCorrectAccount()
    {
        // Arrange
        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync("Test Wallet", TestMnemonic, TestPassword);
        await _walletManager.CreateAccountAsync(wallet.Id, "Account 2");
        await _walletManager.SetActiveAccountAsync(wallet.Id, 1);

        // Act
        BurizaWalletAccount? activeAccount = await _walletManager.GetActiveAccountAsync();

        // Assert
        Assert.NotNull(activeAccount);
        Assert.Equal(1, activeAccount.Index);
        Assert.Equal("Account 2", activeAccount.Name);
    }

    [Fact]
    public async Task GetActiveAccountAsync_WhenNoWallet_ReturnsNull()
    {
        // Act
        BurizaWalletAccount? activeAccount = await _walletManager.GetActiveAccountAsync();

        // Assert
        Assert.Null(activeAccount);
    }

    #endregion

    #region Wallet Lifecycle Edge Cases

    [Fact]
    public async Task DeleteActiveWallet_WithMultipleWallets_SwitchesToRemaining()
    {
        // Arrange
        BurizaWallet wallet1 = await _walletManager.CreateFromMnemonicAsync("Wallet 1", TestMnemonic, TestPassword);
        BurizaWallet wallet2 = await _walletManager.CreateFromMnemonicAsync("Wallet 2", TestMnemonic, TestPassword);

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
    public async Task CreateFromMnemonicAsync_WithDifferentNetworks_CreatesCorrectAddresses()
    {
        // Act
        BurizaWallet mainnetWallet = await _walletManager.CreateFromMnemonicAsync(
            "Mainnet Wallet", TestMnemonic, TestPassword, ChainRegistryData.CardanoMainnet);
        BurizaWallet preprodWallet = await _walletManager.CreateFromMnemonicAsync(
            "Preprod Wallet", TestMnemonic, TestPassword, ChainRegistryData.CardanoPreprod);

        // Assert - Different networks should have different address prefixes
        string? mainnetAddr = mainnetWallet.GetAddressInfo()?.ReceiveAddress;
        string? preprodAddr = preprodWallet.GetAddressInfo()?.ReceiveAddress;

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
        await _walletManager.CreateFromMnemonicAsync("Wallet 1", TestMnemonic, TestPassword);
        await _walletManager.CreateFromMnemonicAsync("Wallet 2", TestMnemonic, TestPassword);

        // Act
        IReadOnlyList<BurizaWallet> wallets = await _walletManager.GetAllAsync();

        // Assert - All wallets should be able to get their primary address (requires provider)
        Assert.Equal(2, wallets.Count);
        foreach (BurizaWallet wallet in wallets)
        {
            Assert.NotNull(wallet.GetAddressInfo()?.ReceiveAddress);
        }
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        InMemoryPlatformStorage platformStorage = new();
        TestWalletStorageService storage = new(platformStorage);
        ChainProviderSettings settings = new()
        {
            Cardano = new CardanoSettings
            {
                MainnetEndpoint = "https://test.example.com",
                PreprodEndpoint = "https://test.example.com",
                PreviewEndpoint = "https://test.example.com",
                MainnetApiKey = "test-key",
                PreprodApiKey = "test-key",
                PreviewApiKey = "test-key"
            }
        };
        BurizaChainProviderFactory factory = new(new TestAppStateService(), settings);
        WalletManagerService manager = new(storage, factory);

        // Act & Assert - Should not throw
        manager.Dispose();
        manager.Dispose();
        manager.Dispose();
        factory.Dispose();
    }

    #endregion
}
