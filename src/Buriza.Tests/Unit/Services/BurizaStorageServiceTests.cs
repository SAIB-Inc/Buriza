using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Crypto;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;
using Buriza.Core.Storage;
using Buriza.Tests.Mocks;

namespace Buriza.Tests.Unit.Services;

public class BurizaStorageServiceTests
{
    [Fact]
    public async Task UnlockVaultAsync_RepeatedFailures_TriggersLockout()
    {
        InMemoryStorage platformStorage = new();
        TestWalletStorageService storage = new(platformStorage);

        Guid walletId = Guid.NewGuid();
        await storage.CreateVaultAsync(walletId, Encoding.UTF8.GetBytes("word1 word2 word3"), Encoding.UTF8.GetBytes("correct"));

        for (int i = 0; i < 5; i++)
        {
            await Assert.ThrowsAnyAsync<CryptographicException>(() => storage.UnlockVaultAsync(walletId, "wrong"));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.UnlockVaultAsync(walletId, "wrong"));
    }

    [Fact]
    public async Task GetCustomProviderConfigWithApiKeyAsync_InvalidMetadata_Throws()
    {
        InMemoryStorage platformStorage = new();
        TestWalletStorageService storage = new(platformStorage);

        ChainInfo chainInfo = ChainRegistry.CardanoMainnet;
        string password = "correct";
        string apiKey = "test-key";

        EncryptedVault vault = VaultEncryption.Encrypt(Guid.Empty, Encoding.UTF8.GetBytes(apiKey), Encoding.UTF8.GetBytes(password), VaultPurpose.Mnemonic);
        string key = StorageKeys.ApiKeyVault((int)chainInfo.Chain, (int)chainInfo.Network);
        await platformStorage.SetAsync(key, System.Text.Json.JsonSerializer.Serialize(vault));

        CustomProviderConfig config = new()
        {
            Chain = chainInfo.Chain,
            Network = chainInfo.Network,
            Endpoint = "https://example.com",
            HasCustomApiKey = true
        };
        await platformStorage.SetAsync(StorageKeys.CustomConfigs, System.Text.Json.JsonSerializer.Serialize(
            new Dictionary<string, CustomProviderConfig>
            {
                [$"{(int)chainInfo.Chain}:{(int)chainInfo.Network}"] = config
            }));

        await Assert.ThrowsAnyAsync<CryptographicException>(() =>
            storage.GetCustomProviderConfigWithApiKeyAsync(chainInfo, password));
    }

    [Fact]
    public async Task SaveCustomProviderConfigAsync_BindsPurposeAndChain()
    {
        InMemoryStorage platformStorage = new();
        TestWalletStorageService storage = new(platformStorage);

        ChainInfo chainInfo = ChainRegistry.CardanoMainnet;
        string password = "correct";
        string apiKey = "test-key";

        CustomProviderConfig config = new()
        {
            Chain = chainInfo.Chain,
            Network = chainInfo.Network,
            Endpoint = "https://example.com",
            HasCustomApiKey = true
        };
        await storage.SaveCustomProviderConfigAsync(config, apiKey, password);

        string key = StorageKeys.ApiKeyVault((int)chainInfo.Chain, (int)chainInfo.Network);
        string? json = await platformStorage.GetAsync(key);
        Assert.NotNull(json);

        EncryptedVault? vault = System.Text.Json.JsonSerializer.Deserialize<EncryptedVault>(json!);
        Assert.NotNull(vault);
        Assert.Equal(VaultPurpose.ApiKey, vault!.Purpose);

        Guid expected = DeriveApiKeyVaultId(chainInfo);
        Assert.Equal(expected, vault.WalletId);
    }

    [Fact]
    public async Task LoadAllWalletsAsync_InvalidJson_Throws()
    {
        InMemoryStorage platformStorage = new();
        await platformStorage.SetAsync(StorageKeys.Wallets, "{not json");

        TestWalletStorageService storage = new(platformStorage);

        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.LoadAllWalletsAsync());
    }

    private static Guid DeriveApiKeyVaultId(ChainInfo chainInfo)
    {
        string input = $"apikey|{(int)chainInfo.Chain}|{(int)chainInfo.Network}";
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hash = SHA256.HashData(bytes);
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }
}
