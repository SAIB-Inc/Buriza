using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Interfaces;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Transaction;
using Buriza.Core.Models.Wallet;
using Buriza.Core.Storage;
using NSubstitute;
using System.Reflection;
using System.Text;

namespace Buriza.Tests.Unit.Models;

public class BurizaWalletTests
{
    private static readonly byte[] FakeMnemonic = Encoding.UTF8.GetBytes("fake mnemonic");

    #region BurizaWallet Properties Tests

    [Fact]
    public void BurizaWallet_RequiredProperties_CanBeSet()
    {
        // Arrange & Act
        BurizaWallet wallet = new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Profile = new WalletProfile { Name = "Test Wallet" }
        };

        // Assert
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), wallet.Id);
        Assert.Equal("Test Wallet", wallet.Profile.Name);
    }

    [Fact]
    public void BurizaWallet_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        BurizaWallet wallet = new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Profile = new WalletProfile { Name = "Test Wallet" }
        };

        // Assert
        Assert.Equal(ChainType.Cardano, wallet.ActiveChain);
        Assert.Equal(0, wallet.ActiveAccountIndex);
        Assert.Empty(wallet.Accounts);
        Assert.Null(wallet.LastAccessedAt);
        Assert.False(wallet.IsUnlocked);
    }

    [Fact]
    public void WalletProfile_AllProperties_CanBeSet()
    {
        // Arrange & Act
        WalletProfile profile = new()
        {
            Name = "My Wallet",
            Label = "Primary",
            Avatar = "avatar.png"
        };

        // Assert
        Assert.Equal("My Wallet", profile.Name);
        Assert.Equal("Primary", profile.Label);
        Assert.Equal("avatar.png", profile.Avatar);
    }

    #endregion

    #region GetActiveAccount Tests

    [Fact]
    public void GetActiveAccount_WhenAccountsEmpty_ReturnsNull()
    {
        // Arrange
        BurizaWallet wallet = new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Profile = new WalletProfile { Name = "Test" } };

        // Act
        BurizaWalletAccount? result = wallet.GetActiveAccount();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetActiveAccount_ReturnsAccountMatchingActiveIndex()
    {
        // Arrange
        BurizaWallet wallet = new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Profile = new WalletProfile { Name = "Test" },
            ActiveAccountIndex = 1,
            Accounts =
            [
                new BurizaWalletAccount { Index = 0, Name = "Account 0" },
                new BurizaWalletAccount { Index = 1, Name = "Account 1" },
                new BurizaWalletAccount { Index = 2, Name = "Account 2" }
            ]
        };

        // Act
        BurizaWalletAccount? result = wallet.GetActiveAccount();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Index);
        Assert.Equal("Account 1", result.Name);
    }

    [Fact]
    public void GetActiveAccount_WhenIndexNotFound_ReturnsNull()
    {
        // Arrange
        BurizaWallet wallet = new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Profile = new WalletProfile { Name = "Test" },
            ActiveAccountIndex = 99,
            Accounts =
            [
                new BurizaWalletAccount { Index = 0, Name = "Account 0" }
            ]
        };

        // Act
        BurizaWalletAccount? result = wallet.GetActiveAccount();

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetAddressInfoAsync Tests

    [Fact]
    public async Task GetAddressInfoAsync_WhenLocked_ReturnsNull()
    {
        // Arrange
        BurizaWallet wallet = new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Profile = new WalletProfile { Name = "Test" } };

        // Act
        ChainAddressData? result = await wallet.GetAddressInfoAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAddressInfoAsync_WhenNoAccount_ReturnsNull()
    {
        // Arrange
        BurizaWallet wallet = CreateUnlockedWallet();

        // Act
        ChainAddressData? result = await wallet.GetAddressInfoAsync();

        // Assert - no accounts exist
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAddressInfoAsync_WhenUnlocked_ReturnsAddress()
    {
        // Arrange
        (BurizaWallet wallet, _) = CreateUnlockedWalletWithMockChainWallet("addr_test1qz...");

        // Act
        ChainAddressData? result = await wallet.GetAddressInfoAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("addr_test1qz...", result.Address);
    }

    [Fact]
    public async Task GetAddressInfoAsync_WithSpecificAccountIndex_ReturnsCorrectData()
    {
        // Arrange
        FakeChainWallet fakeKeyService = new(accountIndex => new ChainAddressData
        {
            ChainInfo = ChainRegistry.CardanoMainnet,
            Address = $"addr_acc{accountIndex}"
        });

        BurizaWallet wallet = new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Profile = new WalletProfile { Name = "Test" },
            ActiveChain = ChainType.Cardano,
            Network = "mainnet",
            ActiveAccountIndex = 0,
            Accounts =
            [
                new BurizaWalletAccount { Index = 0, Name = "Account 0" },
                new BurizaWalletAccount { Index = 1, Name = "Account 1" }
            ]
        };
        AttachChainWallet(wallet, fakeKeyService);
        SetMnemonicBytes(wallet, FakeMnemonic);

        // Act
        ChainAddressData? result = await wallet.GetAddressInfoAsync(accountIndex: 1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("addr_acc1", result.Address);
    }

    #endregion

    #region Lock / Unlock Tests

    [Fact]
    public void Lock_WhenUnlocked_ClearsMnemonic()
    {
        // Arrange
        BurizaWallet wallet = CreateUnlockedWallet();
        Assert.True(wallet.IsUnlocked);

        // Act
        wallet.Lock();

        // Assert
        Assert.False(wallet.IsUnlocked);
    }

    [Fact]
    public void Lock_WhenAlreadyLocked_IsNoOp()
    {
        // Arrange
        BurizaWallet wallet = new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Profile = new WalletProfile { Name = "Test" } };

        // Act & Assert - should not throw
        wallet.Lock();
        Assert.False(wallet.IsUnlocked);
    }

    #endregion

    #region IWallet Query Operations Tests

    [Fact]
    public async Task GetBalanceAsync_WhenLocked_ReturnsZero()
    {
        // Arrange
        BurizaWallet wallet = new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Profile = new WalletProfile { Name = "Test" },
            Accounts = [new BurizaWalletAccount { Index = 0, Name = "Account 0" }]
        };
        AttachMockProvider(wallet);

        // Act
        ulong result = await wallet.GetBalanceAsync();

        // Assert
        Assert.Equal(0UL, result);
    }

    [Fact]
    public async Task GetBalanceAsync_WhenUnlocked_QueriesAddress()
    {
        // Arrange
        (BurizaWallet wallet, IBurizaChainProvider mockProvider) = CreateUnlockedWalletWithMockChainWallet("addr_test1qz...");
        mockProvider.GetBalanceAsync("addr_test1qz...", Arg.Any<CancellationToken>())
            .Returns(5_000_000UL);

        // Act
        ulong result = await wallet.GetBalanceAsync();

        // Assert
        Assert.Equal(5_000_000UL, result);
        await mockProvider.Received(1).GetBalanceAsync("addr_test1qz...", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUtxosAsync_WhenNoProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        BurizaWallet wallet = new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Profile = new WalletProfile { Name = "Test" },
            Accounts = [new BurizaWalletAccount { Index = 0, Name = "Account 0" }]
        };
        SetMnemonicBytes(wallet, FakeMnemonic);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => wallet.GetUtxosAsync());
    }

    [Fact]
    public async Task GetUtxosAsync_WhenUnlocked_QueriesAddress()
    {
        // Arrange
        (BurizaWallet wallet, IBurizaChainProvider mockProvider) = CreateUnlockedWalletWithMockChainWallet("addr_test1qz...");

        List<Utxo> utxos = [new Utxo(TxHash: "tx1", OutputIndex: 0, Value: 1_000_000, Address: "addr_test1qz...")];
        mockProvider.GetUtxosAsync("addr_test1qz...", Arg.Any<CancellationToken>()).Returns(utxos);

        // Act
        IReadOnlyList<Utxo> result = await wallet.GetUtxosAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("tx1", result[0].TxHash);
    }

    [Fact]
    public async Task GetAssetsAsync_WhenNoProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        BurizaWallet wallet = new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Profile = new WalletProfile { Name = "Test" },
            Accounts = [new BurizaWalletAccount { Index = 0, Name = "Account 0" }]
        };
        SetMnemonicBytes(wallet, FakeMnemonic);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => wallet.GetAssetsAsync());
    }


    #endregion

    #region IWallet Transaction Operations Tests

    [Fact]
    public async Task SendAsync_WhenNoProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        BurizaWallet wallet = new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Profile = new WalletProfile { Name = "Test" },
            Accounts = [new BurizaWalletAccount { Index = 0, Name = "Account 0" }]
        };
        SetMnemonicBytes(wallet, FakeMnemonic);

        TransactionRequest request = new(
            Recipients: [new TransactionRecipient("addr_recipient", 1_000_000)]
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => wallet.SendAsync(request));
    }

    #endregion

    #region BurizaWalletAccount Tests

    [Fact]
    public void BurizaWalletAccount_RequiredProperties_CanBeSet()
    {
        // Arrange & Act
        BurizaWalletAccount account = new()
        {
            Index = 0,
            Name = "Main Account"
        };

        // Assert
        Assert.Equal(0, account.Index);
        Assert.Equal("Main Account", account.Name);
    }

    #endregion

    #region ChainAddressData Tests

    [Fact]
    public void ChainAddressData_RequiredProperties_CanBeSet()
    {
        // Arrange & Act
        ChainAddressData data = new()
        {
            ChainInfo = ChainRegistry.CardanoMainnet,
            Address = "addr_test1qz..."
        };

        // Assert
        Assert.Equal(ChainType.Cardano, data.ChainInfo.Chain);
        Assert.Equal("mainnet", data.ChainInfo.Network);
        Assert.Equal("addr_test1qz...", data.Address);
    }

    [Fact]
    public void ChainAddressData_OptionalProperties_HaveDefaults()
    {
        // Arrange & Act
        ChainAddressData data = new()
        {
            ChainInfo = ChainRegistry.CardanoMainnet,
            Address = "addr_test1qz..."
        };

        // Assert
        Assert.Null(data.LastSyncedAt);
    }

    #endregion

    #region Helper Methods

    private static BurizaWallet CreateUnlockedWallet()
    {
        BurizaWallet wallet = new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Profile = new WalletProfile { Name = "Test Wallet" },
            ActiveChain = ChainType.Cardano,
            Network = "mainnet",
            ActiveAccountIndex = 0
        };
        SetMnemonicBytes(wallet, FakeMnemonic);
        return wallet;
    }

    private static (BurizaWallet Wallet, IBurizaChainProvider Provider) CreateUnlockedWalletWithMockChainWallet(string address)
    {
        FakeChainWallet fakeKeyService = new(_ => new ChainAddressData
        {
            ChainInfo = ChainRegistry.CardanoMainnet,
            Address = address
        });

        BurizaWallet wallet = new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Profile = new WalletProfile { Name = "Test Wallet", Avatar = "test.png" },
            ActiveChain = ChainType.Cardano,
            Network = "mainnet",
            ActiveAccountIndex = 0,
            Accounts =
            [
                new BurizaWalletAccount { Index = 0, Name = "Account 0" }
            ]
        };

        IBurizaChainProvider provider = AttachChainWallet(wallet, fakeKeyService);
        SetMnemonicBytes(wallet, FakeMnemonic);
        return (wallet, provider);
    }

    private static void SetMnemonicBytes(BurizaWallet wallet, byte[] mnemonic)
    {
        FieldInfo? field = typeof(BurizaWallet).GetField("_mnemonicBytes",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field!.SetValue(wallet, (byte[])mnemonic.Clone());
    }

    private static IBurizaChainProvider AttachMockProvider(BurizaWallet wallet)
    {
        IBurizaChainProvider provider = Substitute.For<IBurizaChainProvider>();
        IBurizaChainProviderFactory factory = Substitute.For<IBurizaChainProviderFactory>();
        factory.CreateProvider(Arg.Any<ChainInfo>()).Returns(provider);
        SetField(wallet, "_chainProviderFactory", factory);
        return provider;
    }

    private static IBurizaChainProvider AttachChainWallet(BurizaWallet wallet, IChainWallet chainWallet)
    {
        IBurizaChainProvider provider = Substitute.For<IBurizaChainProvider>();
        IBurizaChainProviderFactory factory = Substitute.For<IBurizaChainProviderFactory>();
        factory.CreateProvider(Arg.Any<ChainInfo>()).Returns(provider);
        factory.CreateChainWallet(Arg.Any<ChainInfo>()).Returns(chainWallet);
        SetField(wallet, "_chainProviderFactory", factory);
        return provider;
    }

    private static void SetField(BurizaWallet wallet, string fieldName, object value)
    {
        FieldInfo? field = typeof(BurizaWallet).GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        field!.SetValue(wallet, value);
    }

    #endregion

    /// <summary>
    /// Concrete IChainWallet implementation for tests.
    /// NSubstitute cannot proxy IChainWallet because DeriveChainDataAsync uses ReadOnlySpan&lt;byte&gt;.
    /// </summary>
    private sealed class FakeChainWallet(Func<int, ChainAddressData> addressFactory) : IChainWallet
    {
        public Task<ChainAddressData> DeriveChainDataAsync(ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange = false, CancellationToken ct = default)
            => Task.FromResult(addressFactory(accountIndex));

        public Task<UnsignedTransaction> BuildTransactionAsync(string fromAddress, TransactionRequest request, IBurizaChainProvider provider, CancellationToken ct = default)
            => throw new NotImplementedException();

        public byte[] Sign(UnsignedTransaction unsignedTx, ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo, int accountIndex, int addressIndex)
            => throw new NotImplementedException();
    }
}
