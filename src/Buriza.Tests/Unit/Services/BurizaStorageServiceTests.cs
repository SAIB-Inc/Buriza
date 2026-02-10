using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Crypto;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;
using Buriza.Core.Services;
using Buriza.Core.Storage;
using Buriza.Tests.Mocks;

namespace Buriza.Tests.Unit.Services;

public class BurizaStorageServiceTests
{
    [Fact]
    public async Task UnlockVaultAsync_RepeatedFailures_TriggersLockout()
    {
        InMemoryPlatformStorage platformStorage = new();
        BurizaStorageService storage = new(platformStorage, new NullBiometricService());

        Guid walletId = Guid.NewGuid();
        await storage.CreateVaultAsync(walletId, Encoding.UTF8.GetBytes("word1 word2 word3"), "correct");

        for (int i = 0; i < 5; i++)
        {
            await Assert.ThrowsAnyAsync<CryptographicException>(() => storage.UnlockVaultAsync(walletId, "wrong"));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.UnlockVaultAsync(walletId, "wrong"));
    }

    [Fact]
    public async Task EnablePinAsync_TooShort_Throws()
    {
        InMemoryPlatformStorage platformStorage = new();
        BurizaStorageService storage = new(platformStorage, new NullBiometricService());

        Guid walletId = Guid.NewGuid();
        await storage.CreateVaultAsync(walletId, Encoding.UTF8.GetBytes("word1 word2 word3"), "correct");

        await Assert.ThrowsAsync<ArgumentException>(() => storage.EnablePinAsync(walletId, "1234", "correct"));
    }

    [Fact]
    public async Task UnlockCustomApiKeyAsync_InvalidMetadata_Throws()
    {
        InMemoryPlatformStorage platformStorage = new();
        BurizaStorageService storage = new(platformStorage, new NullBiometricService());

        ChainInfo chainInfo = ChainRegistry.CardanoMainnet;
        string password = "correct";
        string apiKey = "test-key";

        EncryptedVault vault = VaultEncryption.Encrypt(Guid.Empty, Encoding.UTF8.GetBytes(apiKey), password, VaultPurpose.Mnemonic);
        string key = StorageKeys.ApiKeyVault((int)chainInfo.Chain, (int)chainInfo.Network);
        await platformStorage.SetAsync(key, System.Text.Json.JsonSerializer.Serialize(vault));

        await Assert.ThrowsAnyAsync<CryptographicException>(() => storage.UnlockCustomApiKeyAsync(chainInfo, password));
    }

    [Fact]
    public async Task SaveCustomApiKeyAsync_BindsPurposeAndChain()
    {
        InMemoryPlatformStorage platformStorage = new();
        BurizaStorageService storage = new(platformStorage, new NullBiometricService());

        ChainInfo chainInfo = ChainRegistry.CardanoMainnet;
        string password = "correct";
        string apiKey = "test-key";

        await storage.SaveCustomApiKeyAsync(chainInfo, apiKey, password);

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
        InMemoryPlatformStorage platformStorage = new();
        await platformStorage.SetAsync(StorageKeys.Wallets, "{not json");

        BurizaStorageService storage = new(platformStorage, new NullBiometricService());

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
