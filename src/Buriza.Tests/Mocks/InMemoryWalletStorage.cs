using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Crypto;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

namespace Buriza.Tests.Mocks;

/// <summary>
/// In-memory implementation of IWalletStorage for testing.
/// </summary>
public class InMemoryWalletStorage : IWalletStorage
{
    private readonly Dictionary<int, BurizaWallet> _wallets = [];
    private readonly Dictionary<int, EncryptedVault> _vaults = [];
    private readonly Dictionary<string, CustomProviderConfig> _customConfigs = [];
    private readonly Dictionary<string, EncryptedVault> _apiKeyVaults = [];
    private int? _activeWalletId;
    private int _nextId = 1;

    #region Wallet Metadata

    public Task SaveAsync(BurizaWallet wallet, CancellationToken ct = default)
    {
        _wallets[wallet.Id] = wallet;
        return Task.CompletedTask;
    }

    public Task<BurizaWallet?> LoadAsync(int walletId, CancellationToken ct = default)
    {
        _wallets.TryGetValue(walletId, out BurizaWallet? wallet);
        return Task.FromResult(wallet);
    }

    public Task<IReadOnlyList<BurizaWallet>> LoadAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<BurizaWallet>>([.. _wallets.Values]);
    }

    public Task DeleteAsync(int walletId, CancellationToken ct = default)
    {
        _wallets.Remove(walletId);
        return Task.CompletedTask;
    }

    public Task<int?> GetActiveWalletIdAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_activeWalletId);
    }

    public Task SetActiveWalletIdAsync(int walletId, CancellationToken ct = default)
    {
        _activeWalletId = walletId;
        return Task.CompletedTask;
    }

    public Task ClearActiveWalletIdAsync(CancellationToken ct = default)
    {
        _activeWalletId = null;
        return Task.CompletedTask;
    }

    public Task<int> GenerateNextIdAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_nextId++);
    }

    #endregion

    #region Secure Vault

    public Task<bool> HasVaultAsync(int walletId, CancellationToken ct = default)
    {
        return Task.FromResult(_vaults.ContainsKey(walletId));
    }

    public Task CreateVaultAsync(int walletId, byte[] mnemonic, string password, CancellationToken ct = default)
    {
        EncryptedVault vault = VaultEncryption.Encrypt(walletId, mnemonic, password);
        _vaults[walletId] = vault;
        return Task.CompletedTask;
    }

    public Task<byte[]> UnlockVaultAsync(int walletId, string password, CancellationToken ct = default)
    {
        if (!_vaults.TryGetValue(walletId, out EncryptedVault? vault))
            throw new InvalidOperationException($"Vault for wallet {walletId} not found");

        return Task.FromResult(VaultEncryption.Decrypt(vault, password));
    }

    public Task DeleteVaultAsync(int walletId, CancellationToken ct = default)
    {
        _vaults.Remove(walletId);
        return Task.CompletedTask;
    }

    public Task<bool> VerifyPasswordAsync(int walletId, string password, CancellationToken ct = default)
    {
        if (!_vaults.TryGetValue(walletId, out EncryptedVault? vault))
            return Task.FromResult(false);

        return Task.FromResult(VaultEncryption.VerifyPassword(vault, password));
    }

    public async Task ChangePasswordAsync(int walletId, string oldPassword, string newPassword, CancellationToken ct = default)
    {
        byte[] mnemonicBytes = await UnlockVaultAsync(walletId, oldPassword, ct);
        try
        {
            await CreateVaultAsync(walletId, mnemonicBytes, newPassword, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    #endregion

    #region Custom Provider Config

    public Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        string key = GetCustomConfigKey(chainInfo);
        _customConfigs.TryGetValue(key, out CustomProviderConfig? config);
        return Task.FromResult(config);
    }

    public Task SaveCustomProviderConfigAsync(CustomProviderConfig config, CancellationToken ct = default)
    {
        string key = GetCustomConfigKey(config.Chain, config.Network);
        _customConfigs[key] = config;
        return Task.CompletedTask;
    }

    public Task DeleteCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        string key = GetCustomConfigKey(chainInfo);
        _customConfigs.Remove(key);
        _apiKeyVaults.Remove(GetApiKeyVaultKey(chainInfo));
        return Task.CompletedTask;
    }

    public Task SaveCustomApiKeyAsync(ChainInfo chainInfo, string apiKey, string password, CancellationToken ct = default)
    {
        byte[] apiKeyBytes = Encoding.UTF8.GetBytes(apiKey);
        EncryptedVault vault = VaultEncryption.Encrypt(0, apiKeyBytes, password);
        CryptographicOperations.ZeroMemory(apiKeyBytes);
        _apiKeyVaults[GetApiKeyVaultKey(chainInfo)] = vault;

        // Update config to indicate API key exists
        string configKey = GetCustomConfigKey(chainInfo);
        if (_customConfigs.TryGetValue(configKey, out CustomProviderConfig? existing))
        {
            _customConfigs[configKey] = existing with { HasCustomApiKey = true };
        }

        return Task.CompletedTask;
    }

    public Task<byte[]?> UnlockCustomApiKeyAsync(ChainInfo chainInfo, string password, CancellationToken ct = default)
    {
        string key = GetApiKeyVaultKey(chainInfo);
        if (!_apiKeyVaults.TryGetValue(key, out EncryptedVault? vault))
            return Task.FromResult<byte[]?>(null);

        return Task.FromResult<byte[]?>(VaultEncryption.Decrypt(vault, password));
    }

    public Task DeleteCustomApiKeyAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        _apiKeyVaults.Remove(GetApiKeyVaultKey(chainInfo));

        // Update config to indicate API key no longer exists
        string configKey = GetCustomConfigKey(chainInfo);
        if (_customConfigs.TryGetValue(configKey, out CustomProviderConfig? existing) && existing.HasCustomApiKey)
        {
            _customConfigs[configKey] = existing with { HasCustomApiKey = false };
        }

        return Task.CompletedTask;
    }

    private static string GetCustomConfigKey(ChainInfo chainInfo)
        => $"{(int)chainInfo.Chain}:{(int)chainInfo.Network}";

    private static string GetCustomConfigKey(ChainType chain, NetworkType network)
        => $"{(int)chain}:{(int)network}";

    private static string GetApiKeyVaultKey(ChainInfo chainInfo)
        => $"apikey_{(int)chainInfo.Chain}_{(int)chainInfo.Network}";

    #endregion

    #region Custom Data Service Config

    private readonly Dictionary<string, DataServiceConfig> _dataServiceConfigs = [];
    private readonly Dictionary<string, EncryptedVault> _dataServiceApiKeyVaults = [];

    public Task<DataServiceConfig?> GetDataServiceConfigAsync(DataServiceType serviceType, CancellationToken ct = default)
    {
        string key = GetDataServiceConfigKey(serviceType);
        _dataServiceConfigs.TryGetValue(key, out DataServiceConfig? config);
        return Task.FromResult(config);
    }

    public Task<IReadOnlyList<DataServiceConfig>> GetAllDataServiceConfigsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<DataServiceConfig>>([.. _dataServiceConfigs.Values]);
    }

    public Task SaveDataServiceConfigAsync(DataServiceConfig config, CancellationToken ct = default)
    {
        string key = GetDataServiceConfigKey(config.ServiceType);
        _dataServiceConfigs[key] = config;
        return Task.CompletedTask;
    }

    public Task DeleteDataServiceConfigAsync(DataServiceType serviceType, CancellationToken ct = default)
    {
        string key = GetDataServiceConfigKey(serviceType);
        _dataServiceConfigs.Remove(key);
        _dataServiceApiKeyVaults.Remove(GetDataServiceApiKeyVaultKey(serviceType));
        return Task.CompletedTask;
    }

    public Task SaveDataServiceApiKeyAsync(DataServiceType serviceType, string apiKey, string password, CancellationToken ct = default)
    {
        byte[] apiKeyBytes = Encoding.UTF8.GetBytes(apiKey);
        EncryptedVault vault = VaultEncryption.Encrypt(0, apiKeyBytes, password);
        CryptographicOperations.ZeroMemory(apiKeyBytes);
        _dataServiceApiKeyVaults[GetDataServiceApiKeyVaultKey(serviceType)] = vault;

        string configKey = GetDataServiceConfigKey(serviceType);
        if (_dataServiceConfigs.TryGetValue(configKey, out DataServiceConfig? existing))
        {
            _dataServiceConfigs[configKey] = existing with { HasCustomApiKey = true };
        }

        return Task.CompletedTask;
    }

    public Task<byte[]?> UnlockDataServiceApiKeyAsync(DataServiceType serviceType, string password, CancellationToken ct = default)
    {
        string key = GetDataServiceApiKeyVaultKey(serviceType);
        if (!_dataServiceApiKeyVaults.TryGetValue(key, out EncryptedVault? vault))
            return Task.FromResult<byte[]?>(null);

        return Task.FromResult<byte[]?>(VaultEncryption.Decrypt(vault, password));
    }

    public Task DeleteDataServiceApiKeyAsync(DataServiceType serviceType, CancellationToken ct = default)
    {
        _dataServiceApiKeyVaults.Remove(GetDataServiceApiKeyVaultKey(serviceType));

        string configKey = GetDataServiceConfigKey(serviceType);
        if (_dataServiceConfigs.TryGetValue(configKey, out DataServiceConfig? existing) && existing.HasCustomApiKey)
        {
            _dataServiceConfigs[configKey] = existing with { HasCustomApiKey = false };
        }

        return Task.CompletedTask;
    }

    private static string GetDataServiceConfigKey(DataServiceType serviceType)
        => $"data:{(int)serviceType}";

    private static string GetDataServiceApiKeyVaultKey(DataServiceType serviceType)
        => $"data_apikey_{(int)serviceType}";

    #endregion

    #region Test Helpers

    public void Clear()
    {
        _wallets.Clear();
        _vaults.Clear();
        _customConfigs.Clear();
        _apiKeyVaults.Clear();
        _dataServiceConfigs.Clear();
        _dataServiceApiKeyVaults.Clear();
        _activeWalletId = null;
        _nextId = 1;
    }

    public int WalletCount => _wallets.Count;
    public int VaultCount => _vaults.Count;
    public int CustomConfigCount => _customConfigs.Count;
    public int DataServiceConfigCount => _dataServiceConfigs.Count;

    #endregion
}
