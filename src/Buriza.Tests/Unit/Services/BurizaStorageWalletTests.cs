using System.Text;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Wallet;
using Buriza.Core.Storage;
using Buriza.Tests.Mocks;

namespace Buriza.Tests.Unit.Services;

public class BurizaStorageWalletTests
{
    private static readonly byte[] TestMnemonic = Encoding.UTF8.GetBytes(
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
    private static readonly byte[] TestPassword = Encoding.UTF8.GetBytes("TestPassword123!");

    private static (InMemoryStorage Platform, TestWalletStorageService Storage) CreateStorage()
    {
        InMemoryStorage platform = new();
        TestWalletStorageService storage = new(platform);
        return (platform, storage);
    }

    private static BurizaWallet CreateWallet(string name = "Test", string network = "mainnet")
    {
        return new BurizaWallet
        {
            Id = Guid.NewGuid(),
            Profile = new WalletProfile { Name = name },
            Network = network,
            ActiveChain = ChainType.Cardano,
            ActiveAccountIndex = 0,
            Accounts = [new BurizaWalletAccount { Index = 0, Name = "Account 1" }]
        };
    }

    #region Wallet Metadata CRUD

    [Fact]
    public async Task SaveWalletAsync_And_LoadWalletAsync_RoundTrip()
    {
        (_, TestWalletStorageService storage) = CreateStorage();
        BurizaWallet wallet = CreateWallet("RoundTrip Wallet");

        await storage.SaveWalletAsync(wallet);
        BurizaWallet? loaded = await storage.LoadWalletAsync(wallet.Id);

        Assert.NotNull(loaded);
        Assert.Equal(wallet.Id, loaded!.Id);
        Assert.Equal(wallet.Profile.Name, loaded.Profile.Name);
        Assert.Equal(wallet.Network, loaded.Network);
        Assert.Equal(wallet.ActiveChain, loaded.ActiveChain);
        Assert.Equal(wallet.ActiveAccountIndex, loaded.ActiveAccountIndex);
        Assert.Single(loaded.Accounts);
        Assert.Equal(wallet.Accounts[0].Index, loaded.Accounts[0].Index);
        Assert.Equal(wallet.Accounts[0].Name, loaded.Accounts[0].Name);
    }

    [Fact]
    public async Task LoadWalletAsync_NonExistentId_ReturnsNull()
    {
        (_, TestWalletStorageService storage) = CreateStorage();

        BurizaWallet? loaded = await storage.LoadWalletAsync(Guid.NewGuid());

        Assert.Null(loaded);
    }

    [Fact]
    public async Task LoadAllWalletsAsync_MultipleWallets_ReturnsAll()
    {
        (_, TestWalletStorageService storage) = CreateStorage();
        BurizaWallet wallet1 = CreateWallet("Wallet 1");
        BurizaWallet wallet2 = CreateWallet("Wallet 2");
        BurizaWallet wallet3 = CreateWallet("Wallet 3");

        await storage.SaveWalletAsync(wallet1);
        await storage.SaveWalletAsync(wallet2);
        await storage.SaveWalletAsync(wallet3);

        IReadOnlyList<BurizaWallet> all = await storage.LoadAllWalletsAsync();

        Assert.Equal(3, all.Count);
        Assert.Contains(all, w => w.Id == wallet1.Id);
        Assert.Contains(all, w => w.Id == wallet2.Id);
        Assert.Contains(all, w => w.Id == wallet3.Id);
    }

    [Fact]
    public async Task LoadAllWalletsAsync_Empty_ReturnsEmptyList()
    {
        (_, TestWalletStorageService storage) = CreateStorage();

        IReadOnlyList<BurizaWallet> all = await storage.LoadAllWalletsAsync();

        Assert.Empty(all);
    }

    [Fact]
    public async Task DeleteWalletAsync_RemovesWallet()
    {
        (_, TestWalletStorageService storage) = CreateStorage();
        BurizaWallet wallet = CreateWallet();

        await storage.SaveWalletAsync(wallet);
        await storage.DeleteWalletAsync(wallet.Id);

        BurizaWallet? loaded = await storage.LoadWalletAsync(wallet.Id);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task DeleteWalletAsync_DoesNotAffectOtherWallets()
    {
        (_, TestWalletStorageService storage) = CreateStorage();
        BurizaWallet wallet1 = CreateWallet("Keep");
        BurizaWallet wallet2 = CreateWallet("Delete");

        await storage.SaveWalletAsync(wallet1);
        await storage.SaveWalletAsync(wallet2);
        await storage.DeleteWalletAsync(wallet2.Id);

        BurizaWallet? kept = await storage.LoadWalletAsync(wallet1.Id);
        BurizaWallet? deleted = await storage.LoadWalletAsync(wallet2.Id);

        Assert.NotNull(kept);
        Assert.Equal("Keep", kept!.Profile.Name);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task SaveWalletAsync_OverwritesExisting()
    {
        (_, TestWalletStorageService storage) = CreateStorage();
        BurizaWallet wallet = CreateWallet("Original");

        await storage.SaveWalletAsync(wallet);
        wallet.Profile.Name = "Updated";
        await storage.SaveWalletAsync(wallet);

        BurizaWallet? loaded = await storage.LoadWalletAsync(wallet.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Updated", loaded!.Profile.Name);
    }

    #endregion

    #region Active Wallet

    [Fact]
    public async Task SetActiveWalletIdAsync_And_GetActiveWalletIdAsync_RoundTrip()
    {
        (_, TestWalletStorageService storage) = CreateStorage();
        Guid walletId = Guid.NewGuid();

        await storage.SetActiveWalletIdAsync(walletId);
        Guid? active = await storage.GetActiveWalletIdAsync();

        Assert.NotNull(active);
        Assert.Equal(walletId, active!.Value);
    }

    [Fact]
    public async Task GetActiveWalletIdAsync_WhenNotSet_ReturnsNull()
    {
        (_, TestWalletStorageService storage) = CreateStorage();

        Guid? active = await storage.GetActiveWalletIdAsync();

        Assert.Null(active);
    }

    [Fact]
    public async Task ClearActiveWalletIdAsync_ClearsActiveWallet()
    {
        (_, TestWalletStorageService storage) = CreateStorage();
        Guid walletId = Guid.NewGuid();

        await storage.SetActiveWalletIdAsync(walletId);
        await storage.ClearActiveWalletIdAsync();

        Guid? active = await storage.GetActiveWalletIdAsync();
        Assert.Null(active);
    }

    #endregion

    #region Vault Lifecycle

    [Fact]
    public async Task HasVaultAsync_AfterCreate_ReturnsTrue()
    {
        (_, TestWalletStorageService storage) = CreateStorage();
        Guid walletId = Guid.NewGuid();

        await storage.CreateVaultAsync(walletId, TestMnemonic, TestPassword);

        bool hasVault = await storage.HasVaultAsync(walletId);
        Assert.True(hasVault);
    }

    [Fact]
    public async Task HasVaultAsync_BeforeCreate_ReturnsFalse()
    {
        (_, TestWalletStorageService storage) = CreateStorage();

        bool hasVault = await storage.HasVaultAsync(Guid.NewGuid());
        Assert.False(hasVault);
    }

    [Fact]
    public async Task DeleteVaultAsync_RemovesVault()
    {
        (_, TestWalletStorageService storage) = CreateStorage();
        Guid walletId = Guid.NewGuid();

        await storage.CreateVaultAsync(walletId, TestMnemonic, TestPassword);
        await storage.DeleteVaultAsync(walletId);

        bool hasVault = await storage.HasVaultAsync(walletId);
        Assert.False(hasVault);
    }

    [Fact]
    public async Task DeleteVaultAsync_CleansUpAuthState()
    {
        (InMemoryStorage platform, TestWalletStorageService storage) = CreateStorage();
        Guid walletId = Guid.NewGuid();

        await storage.CreateVaultAsync(walletId, TestMnemonic, TestPassword);

        await platform.SetAsync(StorageKeys.EnabledAuthMethods(walletId), "some_value");
        await platform.SetAsync(StorageKeys.LockoutState(walletId), "some_value");

        await storage.DeleteVaultAsync(walletId);

        bool authMethodsExist = await platform.ExistsAsync(StorageKeys.EnabledAuthMethods(walletId));
        bool lockoutExists = await platform.ExistsAsync(StorageKeys.LockoutState(walletId));

        Assert.False(authMethodsExist);
        Assert.False(lockoutExists);
    }

    #endregion

    #region Password Operations

    [Fact]
    public async Task VerifyPasswordAsync_CorrectPassword_ReturnsTrue()
    {
        (_, TestWalletStorageService storage) = CreateStorage();
        Guid walletId = Guid.NewGuid();

        await storage.CreateVaultAsync(walletId, TestMnemonic, TestPassword);

        bool result = await storage.VerifyPasswordAsync(walletId, TestPassword);
        Assert.True(result);
    }

    [Fact]
    public async Task VerifyPasswordAsync_WrongPassword_ReturnsFalse()
    {
        (_, TestWalletStorageService storage) = CreateStorage();
        Guid walletId = Guid.NewGuid();

        await storage.CreateVaultAsync(walletId, TestMnemonic, TestPassword);

        byte[] wrongPassword = Encoding.UTF8.GetBytes("WrongPassword!");
        bool result = await storage.VerifyPasswordAsync(walletId, wrongPassword);
        Assert.False(result);
    }

    [Fact]
    public async Task ChangePasswordAsync_CanUnlockWithNewPassword()
    {
        (_, TestWalletStorageService storage) = CreateStorage();
        Guid walletId = Guid.NewGuid();

        await storage.CreateVaultAsync(walletId, TestMnemonic, TestPassword);

        byte[] newPassword = Encoding.UTF8.GetBytes("NewPassword456!");
        await storage.ChangePasswordAsync(walletId, TestPassword, newPassword);

        byte[] mnemonic = await storage.UnlockVaultAsync(walletId, newPassword);
        Assert.Equal(TestMnemonic, mnemonic);
    }

    [Fact]
    public async Task ChangePasswordAsync_OldPasswordFails()
    {
        (_, TestWalletStorageService storage) = CreateStorage();
        Guid walletId = Guid.NewGuid();

        await storage.CreateVaultAsync(walletId, TestMnemonic, TestPassword);

        byte[] newPassword = Encoding.UTF8.GetBytes("NewPassword456!");
        await storage.ChangePasswordAsync(walletId, TestPassword, newPassword);

        bool result = await storage.VerifyPasswordAsync(walletId, TestPassword);
        Assert.False(result);
    }

    #endregion
}
