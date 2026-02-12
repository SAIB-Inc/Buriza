using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Interfaces;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Transaction;
using Buriza.Core.Models.Wallet;
using NSubstitute;
using System.Reflection;

namespace Buriza.Tests.Unit.Models;

public class BurizaWalletTests
{
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

    #region GetAddressInfo Tests

    [Fact]
    public void GetAddressInfo_WhenNoAccount_ReturnsNull()
    {
        // Arrange
        BurizaWallet wallet = new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Profile = new WalletProfile { Name = "Test" } };

        // Act
        ChainAddressData? result = wallet.GetAddressInfo();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetAddressInfo_WhenNoChainData_ReturnsNull()
    {
        // Arrange
        BurizaWallet wallet = new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Profile = new WalletProfile { Name = "Test" },
            ActiveChain = ChainType.Cardano,
            Accounts = [new BurizaWalletAccount { Index = 0, Name = "Account 0" }]
        };

        // Act
        ChainAddressData? result = wallet.GetAddressInfo();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetAddressInfo_ReturnsReceiveAddress()
    {
        // Arrange
        BurizaWallet wallet = CreateWalletWithAddress();

        // Act
        ChainAddressData? result = wallet.GetAddressInfo();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("addr_test1qz...", result.ReceiveAddress);
    }

    [Fact]
    public void GetAddressInfo_WithNetworkWithoutData_ReturnsNull()
    {
        // Arrange
        BurizaWallet wallet = CreateWalletWithAddress();
        wallet.Network = NetworkType.Preview;

        // Act
        ChainAddressData? result = wallet.GetAddressInfo();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetAddressInfo_WithSpecificAccountIndex_ReturnsCorrectData()
    {
        // Arrange
        BurizaWallet wallet = new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Profile = new WalletProfile { Name = "Test" },
            ActiveChain = ChainType.Cardano,
            Network = NetworkType.Mainnet,
            ActiveAccountIndex = 0,
            Accounts =
            [
                new BurizaWalletAccount
                {
                    Index = 0,
                    Name = "Account 0",
                    ChainData = new Dictionary<ChainType, Dictionary<NetworkType, ChainAddressData>>
                    {
                        [ChainType.Cardano] = new Dictionary<NetworkType, ChainAddressData>
                        {
                            [NetworkType.Mainnet] = new ChainAddressData
                            {
                                Chain = ChainType.Cardano,
                                Network = NetworkType.Mainnet,
                                ReceiveAddress = "addr_acc0"
                            }
                        }
                    }
                },
                new BurizaWalletAccount
                {
                    Index = 1,
                    Name = "Account 1",
                    ChainData = new Dictionary<ChainType, Dictionary<NetworkType, ChainAddressData>>
                    {
                        [ChainType.Cardano] = new Dictionary<NetworkType, ChainAddressData>
                        {
                            [NetworkType.Mainnet] = new ChainAddressData
                            {
                                Chain = ChainType.Cardano,
                                Network = NetworkType.Mainnet,
                                ReceiveAddress = "addr_acc1"
                            }
                        }
                    }
                }
            ]
        };

        // Act
        ChainAddressData? result = wallet.GetAddressInfo(accountIndex: 1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("addr_acc1", result.ReceiveAddress);
    }

    #endregion

    #region IWallet Query Operations Tests

    [Fact]
    public async Task GetBalanceAsync_WhenNoProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        BurizaWallet wallet = CreateWalletWithAddress();

        // Act & Assert
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => wallet.GetBalanceAsync());
        Assert.Contains("provider", ex.Message.ToLower());
    }

    [Fact]
    public async Task GetBalanceAsync_WhenNoAddress_ReturnsZero()
    {
        // Arrange
        BurizaWallet wallet = new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Profile = new WalletProfile { Name = "Test" } };
        AttachMockProvider(wallet);

        // Act
        ulong result = await wallet.GetBalanceAsync();

        // Assert
        Assert.Equal(0UL, result);
    }

    [Fact]
    public async Task GetBalanceAsync_QueriesReceiveAddress()
    {
        // Arrange
        BurizaWallet wallet = CreateWalletWithAddress();
        IBurizaChainProvider mockProvider = AttachMockProvider(wallet);
        mockProvider.GetBalanceAsync("addr_test1qz...", Arg.Any<CancellationToken>())
            .Returns(5_000_000UL);

        // Act
        ulong result = await wallet.GetBalanceAsync();

        // Assert
        Assert.Equal(5_000_000UL, result);
        await mockProvider.Received(1).GetBalanceAsync("addr_test1qz...", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBalanceAsync_ResolvesProviderFromFactoryUsingCurrentNetwork()
    {
        // Arrange
        BurizaWallet wallet = CreateWalletWithAddress();
        wallet.Accounts[0].ChainData[ChainType.Cardano][NetworkType.Preprod] = new ChainAddressData
        {
            Chain = ChainType.Cardano,
            Network = NetworkType.Preprod,
            ReceiveAddress = "addr_test1preprod..."
        };

        IBurizaChainProvider mainnetProvider = Substitute.For<IBurizaChainProvider>();
        IBurizaChainProvider preprodProvider = Substitute.For<IBurizaChainProvider>();
        mainnetProvider.GetBalanceAsync("addr_test1qz...", Arg.Any<CancellationToken>()).Returns(1_000_000UL);
        preprodProvider.GetBalanceAsync("addr_test1preprod...", Arg.Any<CancellationToken>()).Returns(2_000_000UL);

        IBurizaChainProviderFactory factory = Substitute.For<IBurizaChainProviderFactory>();
        factory.CreateProvider(Arg.Is<ChainInfo>(c => c.Chain == ChainType.Cardano && c.Network == NetworkType.Mainnet))
            .Returns(mainnetProvider);
        factory.CreateProvider(Arg.Is<ChainInfo>(c => c.Chain == ChainType.Cardano && c.Network == NetworkType.Preprod))
            .Returns(preprodProvider);
        AttachMockProviderFactory(wallet, factory);

        // Act
        wallet.Network = NetworkType.Mainnet;
        ulong mainnetBalance = await wallet.GetBalanceAsync();

        wallet.Network = NetworkType.Preprod;
        ulong preprodBalance = await wallet.GetBalanceAsync();

        // Assert
        Assert.Equal(1_000_000UL, mainnetBalance);
        Assert.Equal(2_000_000UL, preprodBalance);
        factory.Received(1).CreateProvider(Arg.Is<ChainInfo>(c => c.Chain == ChainType.Cardano && c.Network == NetworkType.Mainnet));
        factory.Received(1).CreateProvider(Arg.Is<ChainInfo>(c => c.Chain == ChainType.Cardano && c.Network == NetworkType.Preprod));
    }

    [Fact]
    public async Task GetUtxosAsync_WhenNoProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        BurizaWallet wallet = CreateWalletWithAddress();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => wallet.GetUtxosAsync());
    }

    [Fact]
    public async Task GetUtxosAsync_QueriesReceiveAddress()
    {
        // Arrange
        BurizaWallet wallet = CreateWalletWithAddress();
        IBurizaChainProvider mockProvider = AttachMockProvider(wallet);

        List<Utxo> utxos = [new Utxo { TxHash = "tx1", OutputIndex = 0, Address = "addr_test1qz...", Value = 1_000_000 }];
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
        BurizaWallet wallet = CreateWalletWithAddress();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => wallet.GetAssetsAsync());
    }

    [Fact]
    public async Task GetTransactionHistoryAsync_WhenNoProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        BurizaWallet wallet = CreateWalletWithAddress();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => wallet.GetTransactionHistoryAsync());
    }

    #endregion

    #region IWallet Transaction Operations Tests

    [Fact]
    public async Task BuildTransactionAsync_WhenNoProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        BurizaWallet wallet = CreateWalletWithAddress();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => wallet.BuildTransactionAsync(1_000_000, "addr_recipient"));
    }

    [Fact]
    public async Task BuildTransactionAsync_WhenNoAddress_ThrowsInvalidOperationException()
    {
        // Arrange
        BurizaWallet wallet = new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Profile = new WalletProfile { Name = "Test" } };
        AttachMockProvider(wallet);

        // Act & Assert
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => wallet.BuildTransactionAsync(1_000_000, "addr_recipient"));
        Assert.Contains("address", ex.Message.ToLower());
    }

    [Fact]
    public async Task SubmitAsync_WhenNoProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        BurizaWallet wallet = CreateWalletWithAddress();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => wallet.SubmitAsync(null!));
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

    [Fact]
    public void BurizaWalletAccount_GetChainData_WhenExists_ReturnsData()
    {
        // Arrange
        ChainAddressData cardanoData = new()
        {
            Chain = ChainType.Cardano,
            Network = NetworkType.Mainnet,
            ReceiveAddress = "addr_test1..."
        };

        BurizaWalletAccount account = new()
        {
            Index = 0,
            Name = "Test",
            ChainData = new Dictionary<ChainType, Dictionary<NetworkType, ChainAddressData>>
            {
                [ChainType.Cardano] = new Dictionary<NetworkType, ChainAddressData>
                {
                    [NetworkType.Mainnet] = cardanoData
                }
            }
        };

        // Act
        ChainAddressData? result = account.GetChainData(ChainType.Cardano, NetworkType.Mainnet);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ChainType.Cardano, result.Chain);
        Assert.Equal("addr_test1...", result.ReceiveAddress);
    }

    [Fact]
    public void BurizaWalletAccount_GetChainData_WhenNotExists_ReturnsNull()
    {
        // Arrange
        BurizaWalletAccount account = new() { Index = 0, Name = "Test" };

        // Act
        ChainAddressData? result = account.GetChainData(ChainType.Cardano, NetworkType.Mainnet);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region ChainAddressData Tests

    [Fact]
    public void ChainAddressData_RequiredProperties_CanBeSet()
    {
        // Arrange & Act
        ChainAddressData data = new()
        {
            Chain = ChainType.Cardano,
            Network = NetworkType.Mainnet,
            ReceiveAddress = "addr_test1qz..."
        };

        // Assert
        Assert.Equal(ChainType.Cardano, data.Chain);
        Assert.Equal(NetworkType.Mainnet, data.Network);
        Assert.Equal("addr_test1qz...", data.ReceiveAddress);
    }

    [Fact]
    public void ChainAddressData_OptionalProperties_HaveDefaults()
    {
        // Arrange & Act
        ChainAddressData data = new()
        {
            Chain = ChainType.Cardano,
            Network = NetworkType.Mainnet,
            ReceiveAddress = "addr_test1qz..."
        };

        // Assert
        Assert.Null(data.LastSyncedAt);
    }

    #endregion

    #region Helper Methods

    private static BurizaWallet CreateWalletWithAddress()
    {
        return new BurizaWallet
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Profile = new WalletProfile { Name = "Test Wallet", Avatar = "test.png" },
            ActiveChain = ChainType.Cardano,
            Network = NetworkType.Mainnet,
            ActiveAccountIndex = 0,
            Accounts =
            [
                new BurizaWalletAccount
                {
                    Index = 0,
                    Name = "Account 0",
                    ChainData = new Dictionary<ChainType, Dictionary<NetworkType, ChainAddressData>>
                    {
                        [ChainType.Cardano] = new Dictionary<NetworkType, ChainAddressData>
                        {
                            [NetworkType.Mainnet] = new ChainAddressData
                            {
                                Chain = ChainType.Cardano,
                                Network = NetworkType.Mainnet,
                                ReceiveAddress = "addr_test1qz..."
                            }
                        }
                    }
                }
            ]
        };
    }

    private static IBurizaChainProvider AttachMockProvider(BurizaWallet wallet)
    {
        IBurizaChainProvider provider = Substitute.For<IBurizaChainProvider>();
        IBurizaChainProviderFactory factory = Substitute.For<IBurizaChainProviderFactory>();
        factory.CreateProvider(Arg.Any<ChainInfo>()).Returns(provider);
        AttachMockProviderFactory(wallet, factory);
        return provider;
    }

    private static void AttachMockProviderFactory(BurizaWallet wallet, IBurizaChainProviderFactory factory)
    {
        // Internal runtime dependency; tests inject it via reflection.
        FieldInfo? field = typeof(BurizaWallet).GetField("_chainProviderFactory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field!.SetValue(wallet, factory);
    }

    #endregion
}
