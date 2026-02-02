using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buriza.Core.Crypto;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models;
using Buriza.Data.Models.Enums;
using Microsoft.JSInterop;

namespace Buriza.Core.Storage.Web;

public class WebWalletStorage(IJSRuntime jsRuntime) : IWalletStorage
{
    private readonly IJSRuntime _js = jsRuntime;

    private const string WalletsKey = "buriza_wallets";
    private const string ActiveWalletKey = "buriza_active_wallet";
    private const string CustomConfigsKey = "buriza_custom_configs";
    private const string DataServiceConfigsKey = "buriza_data_service_configs";

    #region Wallet Metadata

    public async Task SaveAsync(BurizaWallet wallet, CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = [.. await LoadAllAsync(ct)];
        int existingIndex = wallets.FindIndex(w => w.Id == wallet.Id);

        if (existingIndex >= 0)
            wallets[existingIndex] = wallet;
        else
            wallets.Add(wallet);

        string json = JsonSerializer.Serialize(wallets);
        await _js.InvokeVoidAsync("localStorage.setItem", ct, WalletsKey, json);
    }

    public async Task<BurizaWallet?> LoadAsync(int walletId, CancellationToken ct = default)
    {
        IReadOnlyList<BurizaWallet> wallets = await LoadAllAsync(ct);
        return wallets.FirstOrDefault(w => w.Id == walletId);
    }

    public async Task<IReadOnlyList<BurizaWallet>> LoadAllAsync(CancellationToken ct = default)
    {
        string? json = await _js.InvokeAsync<string?>("localStorage.getItem", ct, WalletsKey);
        if (string.IsNullOrEmpty(json))
            return [];

        return JsonSerializer.Deserialize<List<BurizaWallet>>(json) ?? [];
    }

    public async Task DeleteAsync(int walletId, CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = [.. await LoadAllAsync(ct)];
        wallets.RemoveAll(w => w.Id == walletId);

        string json = JsonSerializer.Serialize(wallets);
        await _js.InvokeVoidAsync("localStorage.setItem", ct, WalletsKey, json);
    }

    public async Task<int?> GetActiveWalletIdAsync(CancellationToken ct = default)
    {
        string? value = await _js.InvokeAsync<string?>("localStorage.getItem", ct, ActiveWalletKey);
        if (string.IsNullOrEmpty(value))
            return null;

        return int.TryParse(value, out int id) ? id : null;
    }

    public async Task SetActiveWalletIdAsync(int walletId, CancellationToken ct = default)
    {
        await _js.InvokeVoidAsync("localStorage.setItem", ct, ActiveWalletKey, walletId.ToString());
    }

    public async Task ClearActiveWalletIdAsync(CancellationToken ct = default)
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", ct, ActiveWalletKey);
    }

    public async Task<int> GenerateNextIdAsync(CancellationToken ct = default)
    {
        IReadOnlyList<BurizaWallet> wallets = await LoadAllAsync(ct);
        return wallets.Count > 0 ? wallets.Max(w => w.Id) + 1 : 1;
    }

    #endregion

    #region Secure Vault

    public async Task<bool> HasVaultAsync(int walletId, CancellationToken ct = default)
    {
        string? data = await _js.InvokeAsync<string?>("localStorage.getItem", ct, GetVaultKey(walletId));
        return !string.IsNullOrEmpty(data);
    }

    public async Task CreateVaultAsync(int walletId, string mnemonic, string password, CancellationToken ct = default)
    {
        EncryptedVault vault = VaultEncryption.Encrypt(walletId, mnemonic, password);
        string json = JsonSerializer.Serialize(vault);
        await _js.InvokeVoidAsync("localStorage.setItem", ct, GetVaultKey(walletId), json);
    }

    public async Task<byte[]> UnlockVaultAsync(int walletId, string password, CancellationToken ct = default)
    {
        string? json = await _js.InvokeAsync<string?>("localStorage.getItem", ct, GetVaultKey(walletId));
        if (string.IsNullOrEmpty(json))
            throw new InvalidOperationException($"Vault for wallet {walletId} not found");

        EncryptedVault? vault = JsonSerializer.Deserialize<EncryptedVault>(json) ?? throw new InvalidOperationException($"Failed to deserialize vault for wallet {walletId}");
        return VaultEncryption.DecryptToBytes(vault, password);
    }

    public async Task DeleteVaultAsync(int walletId, CancellationToken ct = default)
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", ct, GetVaultKey(walletId));
    }

    public async Task<bool> VerifyPasswordAsync(int walletId, string password, CancellationToken ct = default)
    {
        string? json = await _js.InvokeAsync<string?>("localStorage.getItem", ct, GetVaultKey(walletId));
        if (string.IsNullOrEmpty(json))
            return false;

        EncryptedVault? vault = JsonSerializer.Deserialize<EncryptedVault>(json);
        if (vault == null) return false;

        return VaultEncryption.VerifyPassword(vault, password);
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

    public async Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainType chain, NetworkType network, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await LoadCustomConfigsAsync(ct);
        string key = GetCustomConfigKey(chain, network);
        return configs.TryGetValue(key, out CustomProviderConfig? config) ? config : null;
    }

    public async Task SaveCustomProviderConfigAsync(CustomProviderConfig config, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await LoadCustomConfigsAsync(ct);
        string key = GetCustomConfigKey(config.Chain, config.Network);
        configs[key] = config;
        await SaveCustomConfigsAsync(configs, ct);
    }

    public async Task DeleteCustomProviderConfigAsync(ChainType chain, NetworkType network, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await LoadCustomConfigsAsync(ct);
        string key = GetCustomConfigKey(chain, network);
        if (configs.Remove(key))
        {
            await SaveCustomConfigsAsync(configs, ct);
        }
        // Also delete the API key
        await DeleteCustomApiKeyAsync(chain, network, ct);
    }

    public async Task SaveCustomApiKeyAsync(ChainType chain, NetworkType network, string apiKey, string password, CancellationToken ct = default)
    {
        // Encrypt API key using same vault encryption
        EncryptedVault vault = VaultEncryption.Encrypt(0, apiKey, password);
        string json = JsonSerializer.Serialize(vault);
        await _js.InvokeVoidAsync("localStorage.setItem", ct, GetApiKeyVaultKey(chain, network), json);

        // Update config to indicate API key exists
        CustomProviderConfig? existing = await GetCustomProviderConfigAsync(chain, network, ct);
        if (existing != null)
        {
            await SaveCustomProviderConfigAsync(existing with { HasCustomApiKey = true }, ct);
        }
    }

    public async Task<byte[]?> UnlockCustomApiKeyAsync(ChainType chain, NetworkType network, string password, CancellationToken ct = default)
    {
        string? json = await _js.InvokeAsync<string?>("localStorage.getItem", ct, GetApiKeyVaultKey(chain, network));
        if (string.IsNullOrEmpty(json))
            return null;

        EncryptedVault? vault = JsonSerializer.Deserialize<EncryptedVault>(json);
        if (vault == null)
            return null;

        return VaultEncryption.DecryptToBytes(vault, password);
    }

    public async Task DeleteCustomApiKeyAsync(ChainType chain, NetworkType network, CancellationToken ct = default)
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", ct, GetApiKeyVaultKey(chain, network));

        // Update config to indicate API key no longer exists
        CustomProviderConfig? existing = await GetCustomProviderConfigAsync(chain, network, ct);
        if (existing is { HasCustomApiKey: true })
        {
            await SaveCustomProviderConfigAsync(existing with { HasCustomApiKey = false }, ct);
        }
    }

    private async Task<Dictionary<string, CustomProviderConfig>> LoadCustomConfigsAsync(CancellationToken ct)
    {
        string? json = await _js.InvokeAsync<string?>("localStorage.getItem", ct, CustomConfigsKey);
        if (string.IsNullOrEmpty(json))
            return [];

        return JsonSerializer.Deserialize<Dictionary<string, CustomProviderConfig>>(json) ?? [];
    }

    private async Task SaveCustomConfigsAsync(Dictionary<string, CustomProviderConfig> configs, CancellationToken ct)
    {
        string json = JsonSerializer.Serialize(configs);
        await _js.InvokeVoidAsync("localStorage.setItem", ct, CustomConfigsKey, json);
    }

    private static string GetCustomConfigKey(ChainType chain, NetworkType network)
        => $"{(int)chain}:{(int)network}";

    private static string GetApiKeyVaultKey(ChainType chain, NetworkType network)
        => $"buriza_apikey_{(int)chain}_{(int)network}";

    #endregion

    #region Custom Data Service Config

    public async Task<DataServiceConfig?> GetDataServiceConfigAsync(DataServiceType serviceType, CancellationToken ct = default)
    {
        Dictionary<string, DataServiceConfig> configs = await LoadDataServiceConfigsAsync(ct);
        string key = GetDataServiceConfigKey(serviceType);
        return configs.TryGetValue(key, out DataServiceConfig? config) ? config : null;
    }

    public async Task<IReadOnlyList<DataServiceConfig>> GetAllDataServiceConfigsAsync(CancellationToken ct = default)
    {
        Dictionary<string, DataServiceConfig> configs = await LoadDataServiceConfigsAsync(ct);
        return [.. configs.Values];
    }

    public async Task SaveDataServiceConfigAsync(DataServiceConfig config, CancellationToken ct = default)
    {
        Dictionary<string, DataServiceConfig> configs = await LoadDataServiceConfigsAsync(ct);
        string key = GetDataServiceConfigKey(config.ServiceType);
        configs[key] = config;
        await SaveDataServiceConfigsAsync(configs, ct);
    }

    public async Task DeleteDataServiceConfigAsync(DataServiceType serviceType, CancellationToken ct = default)
    {
        Dictionary<string, DataServiceConfig> configs = await LoadDataServiceConfigsAsync(ct);
        string key = GetDataServiceConfigKey(serviceType);
        if (configs.Remove(key))
        {
            await SaveDataServiceConfigsAsync(configs, ct);
        }
        await DeleteDataServiceApiKeyAsync(serviceType, ct);
    }

    public async Task SaveDataServiceApiKeyAsync(DataServiceType serviceType, string apiKey, string password, CancellationToken ct = default)
    {
        EncryptedVault vault = VaultEncryption.Encrypt(0, apiKey, password);
        string json = JsonSerializer.Serialize(vault);
        await _js.InvokeVoidAsync("localStorage.setItem", ct, GetDataServiceApiKeyVaultKey(serviceType), json);

        DataServiceConfig? existing = await GetDataServiceConfigAsync(serviceType, ct);
        if (existing != null)
        {
            await SaveDataServiceConfigAsync(existing with { HasCustomApiKey = true }, ct);
        }
    }

    public async Task<byte[]?> UnlockDataServiceApiKeyAsync(DataServiceType serviceType, string password, CancellationToken ct = default)
    {
        string? json = await _js.InvokeAsync<string?>("localStorage.getItem", ct, GetDataServiceApiKeyVaultKey(serviceType));
        if (string.IsNullOrEmpty(json))
            return null;

        EncryptedVault? vault = JsonSerializer.Deserialize<EncryptedVault>(json);
        if (vault == null)
            return null;

        return VaultEncryption.DecryptToBytes(vault, password);
    }

    public async Task DeleteDataServiceApiKeyAsync(DataServiceType serviceType, CancellationToken ct = default)
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", ct, GetDataServiceApiKeyVaultKey(serviceType));

        DataServiceConfig? existing = await GetDataServiceConfigAsync(serviceType, ct);
        if (existing is { HasCustomApiKey: true })
        {
            await SaveDataServiceConfigAsync(existing with { HasCustomApiKey = false }, ct);
        }
    }

    private async Task<Dictionary<string, DataServiceConfig>> LoadDataServiceConfigsAsync(CancellationToken ct)
    {
        string? json = await _js.InvokeAsync<string?>("localStorage.getItem", ct, DataServiceConfigsKey);
        if (string.IsNullOrEmpty(json))
            return [];

        return JsonSerializer.Deserialize<Dictionary<string, DataServiceConfig>>(json) ?? [];
    }

    private async Task SaveDataServiceConfigsAsync(Dictionary<string, DataServiceConfig> configs, CancellationToken ct)
    {
        string json = JsonSerializer.Serialize(configs);
        await _js.InvokeVoidAsync("localStorage.setItem", ct, DataServiceConfigsKey, json);
    }

    private static string GetDataServiceConfigKey(DataServiceType serviceType)
        => $"{(int)serviceType}";

    private static string GetDataServiceApiKeyVaultKey(DataServiceType serviceType)
        => $"buriza_data_apikey_{(int)serviceType}";

    #endregion
}
