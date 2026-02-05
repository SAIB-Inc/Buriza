using System.Text;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Buriza.Core.Providers;
using Buriza.Core.Services;
using Buriza.Data.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using ChainRegistryData = Buriza.Data.Models.Common.ChainRegistry;

namespace Buriza.Tests.Integration.Providers;

public class KeyServiceTests : IDisposable
{
    private readonly IKeyService _keyService;
    private readonly IChainRegistry _chainRegistry;
    private readonly CardanoProvider _testnetProvider;

    private const string TestMnemonicString = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
    private static byte[] TestMnemonicBytes => Encoding.UTF8.GetBytes(TestMnemonicString);

    public KeyServiceTests()
    {
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
        _chainRegistry = new Buriza.Core.Services.ChainRegistry(settings);
        _keyService = new KeyService(_chainRegistry);
        _testnetProvider = new CardanoProvider(
            "https://cardano-preview.utxorpc-m1.demeter.run",
            NetworkType.Preview);
    }

    public void Dispose()
    {
        _testnetProvider.Dispose();
        _chainRegistry.Dispose();
        GC.SuppressFinalize(this);
    }

    #region GenerateMnemonic

    [Fact]
    public void GenerateMnemonic_ReturnsNonEmpty()
    {
        byte[] mnemonicBytes = _keyService.GenerateMnemonic();
        string mnemonic = Encoding.UTF8.GetString(mnemonicBytes);

        Assert.False(string.IsNullOrWhiteSpace(mnemonic));
        Assert.True(mnemonic.Split(' ').Length >= 9);
    }

    [Fact]
    public void GenerateMnemonic_IsUnique()
    {
        string mnemonic1 = Encoding.UTF8.GetString(_keyService.GenerateMnemonic());
        string mnemonic2 = Encoding.UTF8.GetString(_keyService.GenerateMnemonic());

        Assert.NotEqual(mnemonic1, mnemonic2);
    }

    [Fact]
    public void GenerateMnemonic_IsValid()
    {
        byte[] mnemonicBytes = _keyService.GenerateMnemonic();

        Assert.True(_keyService.ValidateMnemonic(mnemonicBytes));
    }

    [Fact]
    public async Task GenerateMnemonic_CanDeriveAddress()
    {
        byte[] mnemonicBytes = _keyService.GenerateMnemonic();

        string address = await _testnetProvider.DeriveAddressAsync(mnemonicBytes, 0, 0);

        Assert.StartsWith("addr_test1", address);
    }

    #endregion

    #region ValidateMnemonic

    [Fact]
    public void ValidateMnemonic_Valid_ReturnsTrue() =>
        Assert.True(_keyService.ValidateMnemonic(TestMnemonicBytes));

    [Fact]
    public void ValidateMnemonic_InvalidWord_ReturnsFalse()
    {
        byte[] invalid = Encoding.UTF8.GetBytes(
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon invalidword");

        Assert.False(_keyService.ValidateMnemonic(invalid));
    }

    [Fact]
    public void ValidateMnemonic_WrongChecksum_ReturnsFalse()
    {
        byte[] wrongChecksum = Encoding.UTF8.GetBytes(
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon");

        Assert.False(_keyService.ValidateMnemonic(wrongChecksum));
    }

    [Fact]
    public void ValidateMnemonic_Empty_ReturnsFalse() =>
        Assert.False(_keyService.ValidateMnemonic([]));

    [Fact]
    public void ValidateMnemonic_TooFewWords_ReturnsFalse() =>
        Assert.False(_keyService.ValidateMnemonic(Encoding.UTF8.GetBytes("abandon abandon abandon")));

    #endregion

    #region DeriveAddress

    [Fact]
    public async Task DeriveAddressAsync_ReturnsBech32()
    {
        string address = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0);

        Assert.StartsWith("addr_test1", address);
        Assert.True(address.Length > 50);
    }

    [Fact]
    public async Task DeriveAddressAsync_IsDeterministic()
    {
        string address1 = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0);
        string address2 = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0);

        Assert.Equal(address1, address2);
    }

    [Fact]
    public async Task DeriveAddressAsync_DifferentIndex_ReturnsDifferent()
    {
        string address0 = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0);
        string address1 = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 1);

        Assert.NotEqual(address0, address1);
    }

    [Fact]
    public async Task DeriveAddressAsync_DifferentAccount_ReturnsDifferent()
    {
        string account0 = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0);
        string account1 = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 1, 0);

        Assert.NotEqual(account0, account1);
    }

    [Fact]
    public async Task DeriveAddressAsync_ChangeAddress_ReturnsDifferent()
    {
        string external = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0, isChange: false);
        string change = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0, isChange: true);

        Assert.NotEqual(external, change);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 5)]
    [InlineData(1, 0)]
    [InlineData(2, 10)]
    public async Task DeriveAddressAsync_VariousIndices_AllValid(int account, int index)
    {
        string address = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, account, index);

        Assert.StartsWith("addr_test1", address);
    }

    #endregion

    #region DerivePrivateKey

    [Fact]
    public async Task DerivePrivateKeyAsync_ReturnsValid()
    {
        PrivateKey key = await _keyService.DerivePrivateKeyAsync(
            TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);

        Assert.NotNull(key);
        Assert.NotEmpty(key.Key);
        Assert.Equal(64, key.Key.Length); // Ed25519 extended: 32 key + 32 chaincode
    }

    [Fact]
    public async Task DerivePrivateKeyAsync_IsDeterministic()
    {
        PrivateKey key1 = await _keyService.DerivePrivateKeyAsync(
            TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);
        PrivateKey key2 = await _keyService.DerivePrivateKeyAsync(
            TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);

        Assert.Equal(key1.Key, key2.Key);
    }

    [Fact]
    public async Task DerivePrivateKeyAsync_DifferentIndex_ReturnsDifferent()
    {
        PrivateKey key0 = await _keyService.DerivePrivateKeyAsync(
            TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);
        PrivateKey key1 = await _keyService.DerivePrivateKeyAsync(
            TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 1);

        Assert.NotEqual(key0.Key, key1.Key);
    }

    #endregion

    #region DerivePublicKey

    [Fact]
    public async Task DerivePublicKeyAsync_ReturnsValid()
    {
        PublicKey key = await _keyService.DerivePublicKeyAsync(
            TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);

        Assert.NotNull(key);
        Assert.NotEmpty(key.Key);
        Assert.Equal(32, key.Key.Length); // Ed25519 public key
    }

    [Fact]
    public async Task DerivePublicKeyAsync_IsDeterministic()
    {
        PublicKey key1 = await _keyService.DerivePublicKeyAsync(
            TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);
        PublicKey key2 = await _keyService.DerivePublicKeyAsync(
            TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);

        Assert.Equal(key1.Key, key2.Key);
    }

    [Fact]
    public async Task DerivePublicKeyAsync_DifferentFromPrivate()
    {
        PrivateKey privateKey = await _keyService.DerivePrivateKeyAsync(
            TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);
        PublicKey publicKey = await _keyService.DerivePublicKeyAsync(
            TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);

        Assert.NotEqual(privateKey.Key, publicKey.Key);
    }

    #endregion

    #region Integration

    [Fact]
    public async Task FullFlow_GenerateValidateDerive()
    {
        byte[] mnemonicBytes = _keyService.GenerateMnemonic();

        Assert.True(_keyService.ValidateMnemonic(mnemonicBytes));

        string address = await _testnetProvider.DeriveAddressAsync(mnemonicBytes, 0, 0);
        Assert.StartsWith("addr_test1", address);
    }

    [Fact]
    public async Task MultipleAddresses_AllUnique()
    {
        List<string> addresses = [];
        for (int i = 0; i < 10; i++)
        {
            addresses.Add(await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, i));
        }

        Assert.Equal(10, addresses.Distinct().Count());
    }

    [Fact]
    public async Task DeriveAddress_DeterministicAcrossInstances()
    {
        using CardanoProvider provider1 = new("https://cardano-preview.utxorpc-m1.demeter.run", NetworkType.Preview);
        using CardanoProvider provider2 = new("https://cardano-preview.utxorpc-m1.demeter.run", NetworkType.Preview);

        string address1 = await provider1.DeriveAddressAsync(TestMnemonicBytes, 0, 0);
        string address2 = await provider2.DeriveAddressAsync(TestMnemonicBytes, 0, 0);

        Assert.Equal(address1, address2);
    }

    [Fact]
    public async Task DeriveAddress_MainnetVsTestnet_DifferentPrefix()
    {
        using CardanoProvider testnet = new("https://cardano-preview.utxorpc-m1.demeter.run", NetworkType.Preview);
        using CardanoProvider mainnet = new("https://cardano-mainnet.utxorpc-m1.demeter.run", NetworkType.Mainnet);

        string testnetAddr = await testnet.DeriveAddressAsync(TestMnemonicBytes, 0, 0);
        string mainnetAddr = await mainnet.DeriveAddressAsync(TestMnemonicBytes, 0, 0);

        Assert.StartsWith("addr_test1", testnetAddr);
        Assert.StartsWith("addr1", mainnetAddr);
    }

    #endregion
}
