using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Wallet;
using Buriza.Core.Services;
using Buriza.Core.Storage;
using Buriza.Data.Models;
using Buriza.Data.Services;
using Buriza.Tests.Mocks;

namespace Buriza.Tests.Integration.Services;

public class WalletLifecycleTests
{
    private const string TestMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
    private const string TestPassword = "TestPassword123!";
    private static readonly byte[] TestPasswordBytes = Encoding.UTF8.GetBytes(TestPassword);

    private static ChainProviderSettings CreateSettings() => new()
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

    private static (InMemoryStorage platformStorage, TestWalletStorageService storage, BurizaChainProviderFactory providerFactory, WalletManagerService walletManager) CreateServices()
    {
        InMemoryStorage platformStorage = new();
        TestWalletStorageService storage = new(platformStorage);
        BurizaChainProviderFactory providerFactory = new(new TestAppStateService(), CreateSettings());
        WalletManagerService walletManager = new(storage, providerFactory);
        return (platformStorage, storage, providerFactory, walletManager);
    }

    private static Task<BurizaWallet> CreateWallet(
        WalletManagerService wm,
        string name = "Test",
        string mnemonic = TestMnemonic,
        string password = TestPassword,
        ChainInfo? chain = null)
        => wm.CreateAsync(name, Encoding.UTF8.GetBytes(mnemonic), Encoding.UTF8.GetBytes(password), chain);

    #region FullLifecycle_Create_Unlock_Query_Lock_Delete

    [Fact]
    public async Task FullLifecycle_Create_Unlock_Query_Lock_Delete()
    {
        var (_, storage, providerFactory, walletManager) = CreateServices();
        try
        {
            BurizaWallet wallet = await CreateWallet(walletManager, "Lifecycle Wallet");
            Assert.True(wallet.IsUnlocked);

            ChainAddressData? addressData = await wallet.GetAddressInfoAsync();
            Assert.NotNull(addressData);
            Assert.False(string.IsNullOrEmpty(addressData!.Address));
            string originalAddress = addressData.Address;

            wallet.Lock();
            Assert.False(wallet.IsUnlocked);
            Assert.Null(await wallet.GetAddressInfoAsync());

            await wallet.UnlockAsync(TestPasswordBytes);
            Assert.True(wallet.IsUnlocked);
            ChainAddressData? unlockedData = await wallet.GetAddressInfoAsync();
            Assert.NotNull(unlockedData);
            Assert.Equal(originalAddress, unlockedData!.Address);

            Guid walletId = wallet.Id;
            await walletManager.DeleteAsync(walletId);

            Assert.Null(await walletManager.GetActiveAsync());
            Assert.Empty(await storage.LoadAllWalletsAsync());
            Assert.False(await storage.HasVaultAsync(walletId));
        }
        finally
        {
            providerFactory.Dispose();
        }
    }

    #endregion

    #region MultipleWallets_CreateAndSwitch

    [Fact]
    public async Task MultipleWallets_CreateAndSwitch()
    {
        var (_, _, providerFactory, walletManager) = CreateServices();
        try
        {
            BurizaWallet walletA = await CreateWallet(walletManager, "Wallet A");
            BurizaWallet? active = await walletManager.GetActiveAsync();
            Assert.NotNull(active);
            Assert.Equal(walletA.Id, active!.Id);

            BurizaWallet walletB = await CreateWallet(walletManager, "Wallet B");
            active = await walletManager.GetActiveAsync();
            Assert.NotNull(active);
            Assert.Equal(walletB.Id, active!.Id);

            IReadOnlyList<BurizaWallet> allWallets = await walletManager.GetAllAsync();
            Assert.Equal(2, allWallets.Count);

            await walletManager.SetActiveAsync(walletA.Id);
            active = await walletManager.GetActiveAsync();
            Assert.NotNull(active);
            Assert.Equal(walletA.Id, active!.Id);

            await walletManager.DeleteAsync(walletA.Id);
            active = await walletManager.GetActiveAsync();
            Assert.NotNull(active);
            Assert.Equal(walletB.Id, active!.Id);
        }
        finally
        {
            providerFactory.Dispose();
        }
    }

    #endregion

    #region PasswordChange_EndToEnd

    [Fact]
    public async Task PasswordChange_EndToEnd()
    {
        var (_, _, providerFactory, walletManager) = CreateServices();
        try
        {
            byte[] newPasswordBytes = Encoding.UTF8.GetBytes("NewPassword456!");

            BurizaWallet wallet = await CreateWallet(walletManager);
            string? originalAddress = (await wallet.GetAddressInfoAsync())?.Address;
            Assert.NotNull(originalAddress);
            wallet.Lock();

            await wallet.ChangePasswordAsync(TestPasswordBytes, newPasswordBytes);

            await wallet.UnlockAsync(newPasswordBytes);
            Assert.True(wallet.IsUnlocked);
            wallet.Lock();

            await Assert.ThrowsAnyAsync<CryptographicException>(() =>
                wallet.UnlockAsync(TestPasswordBytes));

            await wallet.UnlockAsync(newPasswordBytes);
            string? addressAfterChange = (await wallet.GetAddressInfoAsync())?.Address;
            Assert.Equal(originalAddress, addressAfterChange);
        }
        finally
        {
            providerFactory.Dispose();
        }
    }

    #endregion

    #region NetworkSwitch_EndToEnd

    [Fact]
    public async Task NetworkSwitch_EndToEnd()
    {
        var (_, _, providerFactory, walletManager) = CreateServices();
        try
        {
            BurizaWallet wallet = await CreateWallet(walletManager, "Network Test", chain: ChainRegistry.CardanoMainnet);
            Assert.Equal("mainnet", wallet.Network);

            string? mainnetAddress = (await wallet.GetAddressInfoAsync())?.Address;
            Assert.NotNull(mainnetAddress);
            Assert.StartsWith("addr1", mainnetAddress);

            await wallet.SetActiveChainAsync(ChainRegistry.CardanoPreprod);
            BurizaWallet? reloaded = await walletManager.GetActiveAsync();
            Assert.NotNull(reloaded);
            Assert.Equal("preprod", reloaded!.Network);

            await reloaded.UnlockAsync(TestPasswordBytes);
            string? preprodAddress = (await reloaded.GetAddressInfoAsync())?.Address;
            Assert.NotNull(preprodAddress);
            Assert.StartsWith("addr_test1", preprodAddress);
            Assert.NotEqual(mainnetAddress, preprodAddress);

            await reloaded.SetActiveChainAsync(ChainRegistry.CardanoMainnet);
            BurizaWallet? reloaded2 = await walletManager.GetActiveAsync();
            Assert.NotNull(reloaded2);
            await reloaded2!.UnlockAsync(TestPasswordBytes);
            string? mainnetAddress2 = (await reloaded2.GetAddressInfoAsync())?.Address;
            Assert.Equal(mainnetAddress, mainnetAddress2);
        }
        finally
        {
            providerFactory.Dispose();
        }
    }

    #endregion

    #region AccountManagement_CreateAndSwitch

    [Fact]
    public async Task AccountManagement_CreateAndSwitch()
    {
        var (_, _, providerFactory, walletManager) = CreateServices();
        try
        {
            BurizaWallet wallet = await CreateWallet(walletManager, "Account Test");
            Assert.Single(wallet.Accounts);
            Assert.Equal(0, wallet.Accounts[0].Index);

            BurizaWalletAccount account2 = await wallet.CreateAccountAsync("Account 2");
            Assert.Equal(1, account2.Index);

            BurizaWalletAccount account3 = await wallet.CreateAccountAsync("Account 3");
            Assert.Equal(2, account3.Index);

            await wallet.SetActiveAccountAsync(1);
            BurizaWallet? reloaded = await walletManager.GetActiveAsync();
            Assert.NotNull(reloaded);
            Assert.Equal(1, reloaded!.ActiveAccountIndex);

            BurizaWalletAccount? activeAccount = reloaded.GetActiveAccount();
            Assert.NotNull(activeAccount);
            Assert.Equal(1, activeAccount!.Index);

            await reloaded.UnlockAsync(TestPasswordBytes);
            string? addr0 = (await reloaded.GetAddressInfoAsync(0))?.Address;
            string? addr1 = (await reloaded.GetAddressInfoAsync(1))?.Address;
            string? addr2 = (await reloaded.GetAddressInfoAsync(2))?.Address;

            Assert.NotNull(addr0);
            Assert.NotNull(addr1);
            Assert.NotNull(addr2);
            Assert.NotEqual(addr0, addr1);
            Assert.NotEqual(addr1, addr2);
            Assert.NotEqual(addr0, addr2);
        }
        finally
        {
            providerFactory.Dispose();
        }
    }

    #endregion

    #region ExportMnemonic_MatchesOriginal

    [Fact]
    public async Task ExportMnemonic_MatchesOriginal()
    {
        var (_, _, providerFactory, walletManager) = CreateServices();
        try
        {
            BurizaWallet wallet = await CreateWallet(walletManager);

            string? exportedMnemonic = null;
            wallet.ExportMnemonic(mnemonic =>
            {
                exportedMnemonic = mnemonic.ToString();
            });

            Assert.Equal(TestMnemonic, exportedMnemonic);
        }
        finally
        {
            providerFactory.Dispose();
        }
    }

    #endregion

    #region CustomProviderConfig_SaveLoadClear

    [Fact]
    public async Task CustomProviderConfig_SaveLoadClear()
    {
        var (_, _, providerFactory, walletManager) = CreateServices();
        try
        {
            BurizaWallet wallet = await CreateWallet(walletManager);
            Assert.True(wallet.IsUnlocked);

            ChainInfo chain = ChainRegistry.CardanoMainnet;

            await wallet.SetCustomProviderConfigAsync(
                chain,
                "https://custom.endpoint.com",
                "custom-api-key",
                TestPasswordBytes,
                "My Custom Node");

            CustomProviderConfig? config = await wallet.GetCustomProviderConfigAsync(chain);
            Assert.NotNull(config);
            Assert.Equal("https://custom.endpoint.com", config!.Endpoint);
            Assert.Equal("My Custom Node", config.Name);
            Assert.True(config.HasCustomApiKey);

            await wallet.LoadCustomProviderConfigAsync(chain, TestPasswordBytes);

            await wallet.ClearCustomProviderConfigAsync(chain);
            Assert.Null(await wallet.GetCustomProviderConfigAsync(chain));
        }
        finally
        {
            providerFactory.Dispose();
        }
    }

    #endregion

    #region WalletPersistence_SurvivesReload

    [Fact]
    public async Task WalletPersistence_SurvivesReload()
    {
        var (_, _, providerFactory, walletManager) = CreateServices();
        try
        {
            BurizaWallet wallet = await CreateWallet(walletManager, "Persistent Wallet", chain: ChainRegistry.CardanoPreprod);
            await wallet.CreateAccountAsync("Account 2");
            await wallet.SetActiveAccountAsync(1);

            BurizaWallet? reloaded = await walletManager.GetActiveAsync();

            Assert.NotNull(reloaded);
            Assert.Equal("Persistent Wallet", reloaded!.Profile.Name);
            Assert.Equal("preprod", reloaded.Network);
            Assert.Equal(ChainType.Cardano, reloaded.ActiveChain);
            Assert.Equal(2, reloaded.Accounts.Count);
            Assert.Equal(1, reloaded.ActiveAccountIndex);
        }
        finally
        {
            providerFactory.Dispose();
        }
    }

    #endregion

    #region MultipleWalletCreation_UniqueIds

    [Fact]
    public async Task MultipleWalletCreation_UniqueIds()
    {
        var (_, _, providerFactory, walletManager) = CreateServices();
        try
        {
            List<BurizaWallet> createdWallets = [];
            for (int i = 1; i <= 5; i++)
            {
                BurizaWallet w = await CreateWallet(walletManager, $"Wallet {i}");
                createdWallets.Add(w);
            }

            IReadOnlyList<BurizaWallet> allWallets = await walletManager.GetAllAsync();
            Assert.Equal(5, allWallets.Count);

            HashSet<Guid> uniqueIds = [.. allWallets.Select(w => w.Id)];
            Assert.Equal(5, uniqueIds.Count);

            for (int i = 0; i < 5; i++)
            {
                Assert.Equal($"Wallet {i + 1}", createdWallets[i].Profile.Name);
            }

            BurizaWallet? active = await walletManager.GetActiveAsync();
            Assert.NotNull(active);
            Assert.Equal(createdWallets[4].Id, active!.Id);
        }
        finally
        {
            providerFactory.Dispose();
        }
    }

    #endregion
}
