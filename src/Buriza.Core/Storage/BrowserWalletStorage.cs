using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buriza.Core.Crypto;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Microsoft.JSInterop;

namespace Buriza.Core.Storage;

/// <summary>
/// Unified wallet storage for browser environments (web and extension).
/// Calls buriza.storage.* which auto-detects chrome.storage.local (extension) or localStorage (web).
/// Requires storage.js to be loaded.
/// </summary>
public class BrowserWalletStorage(IJSRuntime js) : IWalletStorage
{
    private const string WalletsKey = "buriza_wallets";
    private const string ActiveWalletKey = "buriza_active_wallet";
    private const string CustomConfigsKey = "buriza_custom_configs";
    private const string DataServiceConfigsKey = "buriza_data_service_configs";

    #region Storage Helpers

    private async Task<string?> GetAsync(string key, CancellationToken ct)
        => await js.InvokeAsync<string?>("buriza.storage.get", ct, key);

    private async Task SetAsync(string key, string value, CancellationToken ct)
        => await js.InvokeVoidAsync("buriza.storage.set", ct, key, value);

    private async Task RemoveAsync(string key, CancellationToken ct)
        => await js.InvokeVoidAsync("buriza.storage.remove", ct, key);

    private async Task<T?> GetJsonAsync<T>(string key, CancellationToken ct) where T : class
    {
        string? json = await GetAsync(key, ct);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<T>(json);
    }

    private async Task SetJsonAsync<T>(string key, T value, CancellationToken ct)
        => await SetAsync(key, JsonSerializer.Serialize(value), ct);

    #endregion

    #region Encrypted Vault Helpers

    private async Task SaveEncryptedAsync(string key, string plaintext, string password, CancellationToken ct)
    {
        EncryptedVault vault = VaultEncryption.Encrypt(0, plaintext, password);
        await SetJsonAsync(key, vault, ct);
    }

    private async Task<byte[]?> UnlockEncryptedAsync(string key, string password, CancellationToken ct)
    {
        EncryptedVault? vault = await GetJsonAsync<EncryptedVault>(key, ct);
        return vault is null ? null : VaultEncryption.DecryptToBytes(vault, password);
    }

    #endregion

    #region Wallet Metadata

    public async Task SaveAsync(BurizaWallet wallet, CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = [.. await LoadAllAsync(ct)];
        int existingIndex = wallets.FindIndex(w => w.Id == wallet.Id);

        if (existingIndex >= 0)
            wallets[existingIndex] = wallet;
        else
            wallets.Add(wallet);

        await SetJsonAsync(WalletsKey, wallets, ct);
    }

    public async Task<BurizaWallet?> LoadAsync(int walletId, CancellationToken ct = default)
    {
        IReadOnlyList<BurizaWallet> wallets = await LoadAllAsync(ct);
        return wallets.FirstOrDefault(w => w.Id == walletId);
    }

    public async Task<IReadOnlyList<BurizaWallet>> LoadAllAsync(CancellationToken ct = default)
        => await GetJsonAsync<List<BurizaWallet>>(WalletsKey, ct) ?? [];

    public async Task DeleteAsync(int walletId, CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = [.. await LoadAllAsync(ct)];
        wallets.RemoveAll(w => w.Id == walletId);
        await SetJsonAsync(WalletsKey, wallets, ct);
    }

    public async Task<int?> GetActiveWalletIdAsync(CancellationToken ct = default)
    {
        string? value = await GetAsync(ActiveWalletKey, ct);
        return string.IsNullOrEmpty(value) ? null : int.TryParse(value, out int id) ? id : null;
    }

    public async Task SetActiveWalletIdAsync(int walletId, CancellationToken ct = default)
        => await SetAsync(ActiveWalletKey, walletId.ToString(), ct);

    public async Task ClearActiveWalletIdAsync(CancellationToken ct = default)
        => await RemoveAsync(ActiveWalletKey, ct);

    public async Task<int> GenerateNextIdAsync(CancellationToken ct = default)
    {
        IReadOnlyList<BurizaWallet> wallets = await LoadAllAsync(ct);
        return wallets.Count > 0 ? wallets.Max(w => w.Id) + 1 : 1;
    }

    #endregion

    #region Secure Vault

    public async Task<bool> HasVaultAsync(int walletId, CancellationToken ct = default)
        => !string.IsNullOrEmpty(await GetAsync(GetVaultKey(walletId), ct));

    public async Task CreateVaultAsync(int walletId, string mnemonic, string password, CancellationToken ct = default)
    {
        EncryptedVault vault = VaultEncryption.Encrypt(walletId, mnemonic, password);
        await SetJsonAsync(GetVaultKey(walletId), vault, ct);
    }

    public async Task<byte[]> UnlockVaultAsync(int walletId, string password, CancellationToken ct = default)
    {
        EncryptedVault vault = await GetJsonAsync<EncryptedVault>(GetVaultKey(walletId), ct)
            ?? throw new InvalidOperationException($"Vault for wallet {walletId} not found");

        return VaultEncryption.DecryptToBytes(vault, password);
    }

    public async Task DeleteVaultAsync(int walletId, CancellationToken ct = default)
        => await RemoveAsync(GetVaultKey(walletId), ct);

    public async Task<bool> VerifyPasswordAsync(int walletId, string password, CancellationToken ct = default)
    {
        EncryptedVault? vault = await GetJsonAsync<EncryptedVault>(GetVaultKey(walletId), ct);
        return vault is not null && VaultEncryption.VerifyPassword(vault, password);
    }

    public async Task ChangePasswordAsync(int walletId, string oldPassword, string newPassword, CancellationToken ct = default)
    {
        byte[] mnemonicBytes = await UnlockVaultAsync(walletId, oldPassword, ct);
        try
        {
            string mnemonic = Encoding.UTF8.GetString(mnemonicBytes);
            await CreateVaultAsync(walletId, mnemonic, newPassword, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    private static string GetVaultKey(int walletId) => $"buriza_vault_{walletId}";

    #endregion

    #region Custom Provider Config

    public async Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(CustomConfigsKey, ct) ?? [];
        return configs.GetValueOrDefault(GetCustomConfigKey(chainInfo));
    }

    public async Task SaveCustomProviderConfigAsync(CustomProviderConfig config, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(CustomConfigsKey, ct) ?? [];
        configs[GetCustomConfigKey(config.Chain, config.Network)] = config;
        await SetJsonAsync(CustomConfigsKey, configs, ct);
    }

    public async Task DeleteCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(CustomConfigsKey, ct) ?? [];
        if (configs.Remove(GetCustomConfigKey(chainInfo)))
            await SetJsonAsync(CustomConfigsKey, configs, ct);

        await DeleteCustomApiKeyAsync(chainInfo, ct);
    }

    public async Task SaveCustomApiKeyAsync(ChainInfo chainInfo, string apiKey, string password, CancellationToken ct = default)
    {
        await SaveEncryptedAsync(GetApiKeyVaultKey(chainInfo), apiKey, password, ct);

        CustomProviderConfig? existing = await GetCustomProviderConfigAsync(chainInfo, ct);
        if (existing != null)
            await SaveCustomProviderConfigAsync(existing with { HasCustomApiKey = true }, ct);
    }

    public async Task<byte[]?> UnlockCustomApiKeyAsync(ChainInfo chainInfo, string password, CancellationToken ct = default)
        => await UnlockEncryptedAsync(GetApiKeyVaultKey(chainInfo), password, ct);

    public async Task DeleteCustomApiKeyAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        await RemoveAsync(GetApiKeyVaultKey(chainInfo), ct);

        CustomProviderConfig? existing = await GetCustomProviderConfigAsync(chainInfo, ct);
        if (existing is { HasCustomApiKey: true })
            await SaveCustomProviderConfigAsync(existing with { HasCustomApiKey = false }, ct);
    }

    private static string GetCustomConfigKey(ChainInfo chainInfo)
        => $"{(int)chainInfo.Chain}:{(int)chainInfo.Network}";

    private static string GetCustomConfigKey(ChainType chain, NetworkType network)
        => $"{(int)chain}:{(int)network}";

    private static string GetApiKeyVaultKey(ChainInfo chainInfo)
        => $"buriza_apikey_{(int)chainInfo.Chain}_{(int)chainInfo.Network}";

    #endregion

    #region Custom Data Service Config

    public async Task<DataServiceConfig?> GetDataServiceConfigAsync(DataServiceType serviceType, CancellationToken ct = default)
    {
        Dictionary<string, DataServiceConfig> configs = await GetJsonAsync<Dictionary<string, DataServiceConfig>>(DataServiceConfigsKey, ct) ?? [];
        return configs.GetValueOrDefault(GetDataServiceConfigKey(serviceType));
    }

    public async Task<IReadOnlyList<DataServiceConfig>> GetAllDataServiceConfigsAsync(CancellationToken ct = default)
    {
        Dictionary<string, DataServiceConfig> configs = await GetJsonAsync<Dictionary<string, DataServiceConfig>>(DataServiceConfigsKey, ct) ?? [];
        return [.. configs.Values];
    }

    public async Task SaveDataServiceConfigAsync(DataServiceConfig config, CancellationToken ct = default)
    {
        Dictionary<string, DataServiceConfig> configs = await GetJsonAsync<Dictionary<string, DataServiceConfig>>(DataServiceConfigsKey, ct) ?? [];
        configs[GetDataServiceConfigKey(config.ServiceType)] = config;
        await SetJsonAsync(DataServiceConfigsKey, configs, ct);
    }

    public async Task DeleteDataServiceConfigAsync(DataServiceType serviceType, CancellationToken ct = default)
    {
        Dictionary<string, DataServiceConfig> configs = await GetJsonAsync<Dictionary<string, DataServiceConfig>>(DataServiceConfigsKey, ct) ?? [];
        if (configs.Remove(GetDataServiceConfigKey(serviceType)))
            await SetJsonAsync(DataServiceConfigsKey, configs, ct);

        await DeleteDataServiceApiKeyAsync(serviceType, ct);
    }

    public async Task SaveDataServiceApiKeyAsync(DataServiceType serviceType, string apiKey, string password, CancellationToken ct = default)
    {
        await SaveEncryptedAsync(GetDataServiceApiKeyVaultKey(serviceType), apiKey, password, ct);

        DataServiceConfig? existing = await GetDataServiceConfigAsync(serviceType, ct);
        if (existing != null)
            await SaveDataServiceConfigAsync(existing with { HasCustomApiKey = true }, ct);
    }

    public async Task<byte[]?> UnlockDataServiceApiKeyAsync(DataServiceType serviceType, string password, CancellationToken ct = default)
        => await UnlockEncryptedAsync(GetDataServiceApiKeyVaultKey(serviceType), password, ct);

    public async Task DeleteDataServiceApiKeyAsync(DataServiceType serviceType, CancellationToken ct = default)
    {
        await RemoveAsync(GetDataServiceApiKeyVaultKey(serviceType), ct);

        DataServiceConfig? existing = await GetDataServiceConfigAsync(serviceType, ct);
        if (existing is { HasCustomApiKey: true })
            await SaveDataServiceConfigAsync(existing with { HasCustomApiKey = false }, ct);
    }

    private static string GetDataServiceConfigKey(DataServiceType serviceType)
        => $"{(int)serviceType}";

    private static string GetDataServiceApiKeyVaultKey(DataServiceType serviceType)
        => $"buriza_data_apikey_{(int)serviceType}";

    #endregion
}
