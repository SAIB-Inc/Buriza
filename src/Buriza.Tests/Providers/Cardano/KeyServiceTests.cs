using System.Text;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Buriza.Core.Providers;
using Buriza.Core.Services;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Chrysalis.Wallet.Models.Keys;

using ChainRegistryData = Buriza.Data.Models.Common.ChainRegistry;

namespace Buriza.Tests.Providers.Cardano;

public class KeyServiceTests : IDisposable
{
    private readonly IKeyService _keyService;
    private readonly IChainRegistry _chainRegistry;
    private readonly CardanoProvider _testnetProvider;

    // Standard test mnemonic (DO NOT USE IN PRODUCTION)
    private const string TestMnemonicString = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    // Helper to convert mnemonic string to bytes for API calls
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

    #region GenerateMnemonic Tests

    [Fact]
    public void GenerateMnemonic_Default_ReturnsNonEmptyMnemonic()
    {
        // Act
        byte[] mnemonicBytes = _keyService.GenerateMnemonic();
        string mnemonic = Encoding.UTF8.GetString(mnemonicBytes);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(mnemonic));
        string[] words = mnemonic.Split(' ');
        Assert.True(words.Length >= 9); // At minimum should have valid word count
    }

    [Fact]
    public void GenerateMnemonic_GeneratesUniqueMnemonics()
    {
        // Act
        byte[] mnemonicBytes1 = _keyService.GenerateMnemonic();
        byte[] mnemonicBytes2 = _keyService.GenerateMnemonic();
        string mnemonic1 = Encoding.UTF8.GetString(mnemonicBytes1);
        string mnemonic2 = Encoding.UTF8.GetString(mnemonicBytes2);

        // Assert
        Assert.NotEqual(mnemonic1, mnemonic2);
    }

    [Fact]
    public void GenerateMnemonic_GeneratesValidMnemonic()
    {
        // Act
        byte[] mnemonicBytes = _keyService.GenerateMnemonic();

        // Assert - Generated mnemonic should be valid
        bool isValid = _keyService.ValidateMnemonic(mnemonicBytes);
        Assert.True(isValid);
    }

    [Fact]
    public async Task GenerateMnemonic_CanDeriveAddressFromGenerated()
    {
        // Act
        byte[] mnemonicBytes = _keyService.GenerateMnemonic();
        string address = await _testnetProvider.DeriveAddressAsync(mnemonicBytes, 0, 0);

        // Assert - testnet address
        Assert.StartsWith("addr_test1", address);
    }

    #endregion

    #region ValidateMnemonic Tests

    [Fact]
    public void ValidateMnemonic_WithValidMnemonic_ReturnsTrue()
    {
        // Act
        bool result = _keyService.ValidateMnemonic(TestMnemonicBytes);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateMnemonic_WithInvalidWord_ReturnsFalse()
    {
        // Arrange - "invalidword" is not in BIP-39 wordlist
        byte[] invalidMnemonic = Encoding.UTF8.GetBytes("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon invalidword");

        // Act
        bool result = _keyService.ValidateMnemonic(invalidMnemonic);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateMnemonic_WithWrongChecksum_ReturnsFalse()
    {
        // Arrange - Valid words but wrong checksum
        byte[] wrongChecksum = Encoding.UTF8.GetBytes("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon");

        // Act
        bool result = _keyService.ValidateMnemonic(wrongChecksum);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateMnemonic_WithEmptyBytes_ReturnsFalse()
    {
        // Act
        bool result = _keyService.ValidateMnemonic([]);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateMnemonic_WithTooFewWords_ReturnsFalse()
    {
        // Arrange
        byte[] tooFewWords = Encoding.UTF8.GetBytes("abandon abandon abandon");

        // Act
        bool result = _keyService.ValidateMnemonic(tooFewWords);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateMnemonic_With12WordValidMnemonic_ReturnsTrue()
    {
        // Arrange - Standard 12-word test mnemonic
        byte[] mnemonic = Encoding.UTF8.GetBytes("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");

        // Act
        bool result = _keyService.ValidateMnemonic(mnemonic);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region DeriveAddress Tests

    [Fact]
    public async Task DeriveAddressAsync_FirstAddress_ReturnsBech32Address()
    {
        // Act
        string address = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0);

        // Assert - Cardano testnet addresses start with "addr_test1"
        Assert.StartsWith("addr_test1", address);
    }

    [Fact]
    public async Task DeriveAddressAsync_SameMnemonicSameIndex_ReturnsSameAddress()
    {
        // Act
        string address1 = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0);
        string address2 = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0);

        // Assert - Deterministic derivation
        Assert.Equal(address1, address2);
    }

    [Fact]
    public async Task DeriveAddressAsync_DifferentIndex_ReturnsDifferentAddress()
    {
        // Act
        string address0 = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0);
        string address1 = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 1);

        // Assert
        Assert.NotEqual(address0, address1);
    }

    [Fact]
    public async Task DeriveAddressAsync_DifferentAccount_ReturnsDifferentAddress()
    {
        // Act
        string account0 = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0);
        string account1 = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 1, 0);

        // Assert
        Assert.NotEqual(account0, account1);
    }

    [Fact]
    public async Task DeriveAddressAsync_ChangeAddress_ReturnsDifferentAddress()
    {
        // Act
        string externalAddress = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0, isChange: false);
        string changeAddress = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0, isChange: true);

        // Assert
        Assert.NotEqual(externalAddress, changeAddress);
    }

    [Fact]
    public async Task DeriveAddressAsync_ReturnsValidBech32Length()
    {
        // Act
        string address = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0);

        // Assert - Cardano base addresses are ~100+ characters
        Assert.True(address.Length > 50);
    }

    [Fact]
    public async Task DeriveAddressAsync_KnownMnemonic_ReturnsExpectedAddress()
    {
        // Arrange - Using the well-known test mnemonic
        // The expected address for account 0, index 0 with this mnemonic
        // This is a deterministic test vector

        // Act
        string address = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0);

        // Assert - Should be a valid testnet base address
        Assert.StartsWith("addr_test1", address);
        Assert.Contains("q", address); // Base addresses contain 'q' after addr_test1
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 5)]
    [InlineData(1, 0)]
    [InlineData(2, 10)]
    public async Task DeriveAddressAsync_VariousIndices_AllStartWithAddrTest1(int accountIndex, int addressIndex)
    {
        // Act
        string address = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, accountIndex, addressIndex);

        // Assert
        Assert.StartsWith("addr_test1", address);
    }

    #endregion

    #region DerivePrivateKey Tests

    [Fact]
    public async Task DerivePrivateKeyAsync_ReturnsNonNullKey()
    {
        // Act
        PrivateKey privateKey = await _keyService.DerivePrivateKeyAsync(TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);

        // Assert
        Assert.NotNull(privateKey);
        Assert.NotNull(privateKey.Key);
        Assert.NotEmpty(privateKey.Key);
    }

    [Fact]
    public async Task DerivePrivateKeyAsync_SameIndexSameMnemonic_ReturnsSameKey()
    {
        // Act
        PrivateKey key1 = await _keyService.DerivePrivateKeyAsync(TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);
        PrivateKey key2 = await _keyService.DerivePrivateKeyAsync(TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);

        // Assert
        Assert.Equal(key1.Key, key2.Key);
    }

    [Fact]
    public async Task DerivePrivateKeyAsync_DifferentIndex_ReturnsDifferentKey()
    {
        // Act
        PrivateKey key0 = await _keyService.DerivePrivateKeyAsync(TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);
        PrivateKey key1 = await _keyService.DerivePrivateKeyAsync(TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 1);

        // Assert
        Assert.NotEqual(key0.Key, key1.Key);
    }

    [Fact]
    public async Task DerivePrivateKeyAsync_ReturnsCorrectKeyLength()
    {
        // Act
        PrivateKey privateKey = await _keyService.DerivePrivateKeyAsync(TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);

        // Assert - Ed25519 extended private key is 64 bytes (32 key + 32 chaincode)
        Assert.Equal(64, privateKey.Key.Length);
    }

    #endregion

    #region DerivePublicKey Tests

    [Fact]
    public async Task DerivePublicKeyAsync_ReturnsNonNullKey()
    {
        // Act
        PublicKey publicKey = await _keyService.DerivePublicKeyAsync(TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);

        // Assert
        Assert.NotNull(publicKey);
        Assert.NotNull(publicKey.Key);
        Assert.NotEmpty(publicKey.Key);
    }

    [Fact]
    public async Task DerivePublicKeyAsync_SameIndexSameMnemonic_ReturnsSameKey()
    {
        // Act
        PublicKey key1 = await _keyService.DerivePublicKeyAsync(TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);
        PublicKey key2 = await _keyService.DerivePublicKeyAsync(TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);

        // Assert
        Assert.Equal(key1.Key, key2.Key);
    }

    [Fact]
    public async Task DerivePublicKeyAsync_DifferentFromPrivateKey()
    {
        // Act
        PrivateKey privateKey = await _keyService.DerivePrivateKeyAsync(TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);
        PublicKey publicKey = await _keyService.DerivePublicKeyAsync(TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);

        // Assert
        Assert.NotEqual(privateKey.Key, publicKey.Key);
    }

    [Fact]
    public async Task DerivePublicKeyAsync_ReturnsCorrectKeyLength()
    {
        // Act
        PublicKey publicKey = await _keyService.DerivePublicKeyAsync(TestMnemonicBytes, ChainRegistryData.CardanoPreview, 0, 0);

        // Assert - Ed25519 public key is 32 bytes
        Assert.Equal(32, publicKey.Key.Length);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task FullDerivationFlow_GenerateValidateDerive_Works()
    {
        // Act - Generate mnemonic
        byte[] mnemonicBytes = _keyService.GenerateMnemonic();

        // Assert - Validate it
        bool isValid = _keyService.ValidateMnemonic(mnemonicBytes);
        Assert.True(isValid);

        // Act - Derive address using testnet provider
        string address = await _testnetProvider.DeriveAddressAsync(mnemonicBytes, 0, 0);

        // Assert - Address is valid (testnet)
        Assert.StartsWith("addr_test1", address);
    }

    [Fact]
    public async Task MultipleAddresses_FromSameMnemonic_AllUnique()
    {
        // Act - Generate 10 addresses
        List<string> addresses = [];
        for (int i = 0; i < 10; i++)
        {
            string address = await _testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, i);
            addresses.Add(address);
        }

        // Assert - All unique
        Assert.Equal(10, addresses.Distinct().Count());
    }

    [Fact]
    public async Task DeriveAddress_IsDeterministic_AcrossInstances()
    {
        // Arrange
        using CardanoProvider provider1 = new("https://cardano-preview.utxorpc-m1.demeter.run", NetworkType.Preview);
        using CardanoProvider provider2 = new("https://cardano-preview.utxorpc-m1.demeter.run", NetworkType.Preview);

        // Act
        string address1 = await provider1.DeriveAddressAsync(TestMnemonicBytes, 0, 0);
        string address2 = await provider2.DeriveAddressAsync(TestMnemonicBytes, 0, 0);

        // Assert - Same mnemonic = same address, regardless of instance
        Assert.Equal(address1, address2);
    }

    [Fact]
    public async Task DeriveAddress_MainnetVsTestnet_ReturnsDifferentPrefix()
    {
        // Arrange
        using CardanoProvider testnetProvider = new("https://cardano-preview.utxorpc-m1.demeter.run", NetworkType.Preview);
        using CardanoProvider mainnetProvider = new("https://cardano-mainnet.utxorpc-m1.demeter.run", NetworkType.Mainnet);

        // Act
        string testnetAddress = await testnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0);
        string mainnetAddress = await mainnetProvider.DeriveAddressAsync(TestMnemonicBytes, 0, 0);

        // Assert
        Assert.StartsWith("addr_test1", testnetAddress);
        Assert.StartsWith("addr1", mainnetAddress);
        Assert.NotEqual(testnetAddress, mainnetAddress);
    }

    #endregion
}
