using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buriza.Core.Crypto;
using Buriza.Core.Interfaces.Security;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;
using Buriza.Core.Models.Wallet;
using Buriza.Core.Storage;

namespace Buriza.Core.Services;

/// <summary>
/// Unified storage service that handles all storage operations.
/// Routes storage based on auth type (password/pin/biometric).
/// Implements IStorageProvider and ISecureStorageProvider for DI compatibility.
/// </summary>
public class BurizaStorageService(
    IPlatformStorage platformStorage,
    IBiometricService biometricService) : IStorageProvider, ISecureStorageProvider
{
    private readonly IPlatformStorage _storage = platformStorage;
    private readonly IBiometricService _biometricService = biometricService;

    #region IStorageProvider Implementation

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
        => _storage.GetAsync(key, ct);

    public Task SetAsync(string key, string value, CancellationToken ct = default)
        => _storage.SetAsync(key, value, ct);

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => _storage.RemoveAsync(key, ct);

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => _storage.ExistsAsync(key, ct);

    public Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default)
        => _storage.GetKeysAsync(prefix, ct);

    public Task ClearAsync(CancellationToken ct = default)
        => _storage.ClearAsync(ct);

    #endregion

    #region ISecureStorageProvider Implementation

    // Secure storage uses the same platform storage - data is encrypted by VaultEncryption
    public Task<string?> GetSecureAsync(string key, CancellationToken ct = default)
        => _storage.GetAsync(key, ct);

    public Task SetSecureAsync(string key, string value, CancellationToken ct = default)
        => _storage.SetAsync(key, value, ct);

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
        => _storage.RemoveAsync(key, ct);

    #endregion

    #region Capabilities

    public async Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        bool supportsBiometric = await _biometricService.IsAvailableAsync(ct);
        BiometricType biometricType = supportsBiometric
            ? await _biometricService.GetBiometricTypeAsync(ct) ?? BiometricType.NotAvailable
            : BiometricType.NotAvailable;

        return new DeviceCapabilities(
            SupportsBiometric: supportsBiometric,
            BiometricType: biometricType,
            SupportsPin: true,
            SupportsPassword: true
        );
    }

    #endregion

    #region Auth Type Management

    public async Task<AuthenticationType> GetAuthTypeAsync(Guid walletId, CancellationToken ct = default)
    {
        string? typeStr = await _storage.GetAsync(StorageKeys.AuthType(walletId), ct);
        return typeStr switch
        {
            "biometric" => AuthenticationType.Biometric,
            "pin" => AuthenticationType.Pin,
            _ => AuthenticationType.Password
        };
    }

    public async Task SetAuthTypeAsync(Guid walletId, AuthenticationType type, CancellationToken ct = default)
    {
        string typeStr = type switch
        {
            AuthenticationType.Biometric => "biometric",
            AuthenticationType.Pin => "pin",
            _ => "password"
        };
        await _storage.SetAsync(StorageKeys.AuthType(walletId), typeStr, ct);
    }

    #endregion

    #region Wallet Metadata

    public async Task SaveWalletAsync(BurizaWallet wallet, CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = [.. await LoadAllWalletsAsync(ct)];
        int existingIndex = wallets.FindIndex(w => w.Id == wallet.Id);

        if (existingIndex >= 0)
            wallets[existingIndex] = wallet;
        else
            wallets.Add(wallet);

        await SetJsonAsync(StorageKeys.Wallets, wallets, ct);
    }

    public async Task<BurizaWallet?> LoadWalletAsync(Guid walletId, CancellationToken ct = default)
    {
        IReadOnlyList<BurizaWallet> wallets = await LoadAllWalletsAsync(ct);
        return wallets.FirstOrDefault(w => w.Id == walletId);
    }

    public async Task<IReadOnlyList<BurizaWallet>> LoadAllWalletsAsync(CancellationToken ct = default)
        => await GetJsonAsync<List<BurizaWallet>>(StorageKeys.Wallets, ct) ?? [];

    public async Task DeleteWalletAsync(Guid walletId, CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = [.. await LoadAllWalletsAsync(ct)];
        wallets.RemoveAll(w => w.Id == walletId);
        await SetJsonAsync(StorageKeys.Wallets, wallets, ct);

        await DeleteVaultAsync(walletId, ct);
    }

    public async Task<Guid?> GetActiveWalletIdAsync(CancellationToken ct = default)
    {
        string? value = await _storage.GetAsync(StorageKeys.ActiveWallet, ct);
        return string.IsNullOrEmpty(value) ? null : Guid.TryParse(value, out Guid id) ? id : null;
    }

    public async Task SetActiveWalletIdAsync(Guid walletId, CancellationToken ct = default)
        => await _storage.SetAsync(StorageKeys.ActiveWallet, walletId.ToString("N"), ct);

    public async Task ClearActiveWalletIdAsync(CancellationToken ct = default)
        => await _storage.RemoveAsync(StorageKeys.ActiveWallet, ct);

    #endregion

    #region Vault Operations

    public async Task<bool> HasVaultAsync(Guid walletId, CancellationToken ct = default)
    {
        string? json = await _storage.GetAsync(StorageKeys.Vault(walletId), ct);
        return !string.IsNullOrEmpty(json);
    }

    public async Task CreateVaultAsync(Guid walletId, byte[] mnemonic, string password, CancellationToken ct = default)
    {
        EncryptedVault vault = VaultEncryption.Encrypt(walletId, mnemonic, password);
        await SetJsonAsync(StorageKeys.Vault(walletId), vault, ct);
    }

    public async Task<byte[]> UnlockVaultAsync(Guid walletId, string password, CancellationToken ct = default)
    {
        EncryptedVault vault = await GetJsonAsync<EncryptedVault>(StorageKeys.Vault(walletId), ct)
            ?? throw new InvalidOperationException("Vault not found");

        return VaultEncryption.Decrypt(vault, password);
    }

    /// <summary>
    /// Unlocks vault using biometric authentication.
    /// Retrieves password from secure enclave, then decrypts vault.
    /// </summary>
    public async Task<byte[]> UnlockVaultWithBiometricAsync(Guid walletId, string reason, CancellationToken ct = default)
    {
        byte[] passwordBytes = await AuthenticateWithBiometricAsync(walletId, reason, ct);
        try
        {
            string password = Encoding.UTF8.GetString(passwordBytes);
            return await UnlockVaultAsync(walletId, password, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    /// <summary>
    /// Unlocks vault using PIN authentication.
    /// Decrypts password from PIN vault, then decrypts main vault.
    /// </summary>
    public async Task<byte[]> UnlockVaultWithPinAsync(Guid walletId, string pin, CancellationToken ct = default)
    {
        byte[] passwordBytes = await AuthenticateWithPinAsync(walletId, pin, ct);
        try
        {
            string password = Encoding.UTF8.GetString(passwordBytes);
            return await UnlockVaultAsync(walletId, password, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public async Task<bool> VerifyPasswordAsync(Guid walletId, string password, CancellationToken ct = default)
    {
        EncryptedVault? vault = await GetJsonAsync<EncryptedVault>(StorageKeys.Vault(walletId), ct);
        return vault is not null && VaultEncryption.VerifyPassword(vault, password);
    }

    public async Task ChangePasswordAsync(Guid walletId, string oldPassword, string newPassword, CancellationToken ct = default)
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

    public async Task DeleteVaultAsync(Guid walletId, CancellationToken ct = default)
    {
        await _storage.RemoveAsync(StorageKeys.Vault(walletId), ct);
        await _storage.RemoveAsync(StorageKeys.PinVault(walletId), ct);
        await _biometricService.RemoveSecureAsync(StorageKeys.BiometricKey(walletId), ct);
        await _storage.RemoveAsync(StorageKeys.AuthType(walletId), ct);
        await _storage.RemoveAsync(StorageKeys.LockoutState(walletId), ct);
    }

    #endregion

    #region Biometric Auth

    public async Task<bool> IsBiometricEnabledAsync(Guid walletId, CancellationToken ct = default)
    {
        AuthenticationType authType = await GetAuthTypeAsync(walletId, ct);
        return authType == AuthenticationType.Biometric;
    }

    public async Task EnableBiometricAsync(Guid walletId, string password, CancellationToken ct = default)
    {
        if (!await VerifyPasswordAsync(walletId, password, ct))
            throw new CryptographicException("Invalid password");

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            await _biometricService.StoreSecureAsync(StorageKeys.BiometricKey(walletId), passwordBytes, ct);
            await SetAuthTypeAsync(walletId, AuthenticationType.Biometric, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public async Task DisableBiometricAsync(Guid walletId, CancellationToken ct = default)
    {
        await _biometricService.RemoveSecureAsync(StorageKeys.BiometricKey(walletId), ct);
        await SetAuthTypeAsync(walletId, AuthenticationType.Password, ct);
    }

    public async Task<byte[]> AuthenticateWithBiometricAsync(Guid walletId, string reason, CancellationToken ct = default)
    {
        byte[]? passwordBytes = await _biometricService.RetrieveSecureAsync(StorageKeys.BiometricKey(walletId), reason, ct);
        return passwordBytes ?? throw new CryptographicException("Biometric authentication failed");
    }

    #endregion

    #region PIN Auth

    public async Task<bool> IsPinEnabledAsync(Guid walletId, CancellationToken ct = default)
    {
        AuthenticationType authType = await GetAuthTypeAsync(walletId, ct);
        return authType == AuthenticationType.Pin;
    }

    public async Task EnablePinAsync(Guid walletId, string pin, string password, CancellationToken ct = default)
    {
        if (!await VerifyPasswordAsync(walletId, password, ct))
            throw new CryptographicException("Invalid password");

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            EncryptedVault pinVault = VaultEncryption.Encrypt(walletId, passwordBytes, pin, VaultPurpose.PinProtectedPassword);
            await SetJsonAsync(StorageKeys.PinVault(walletId), pinVault, ct);
            await SetAuthTypeAsync(walletId, AuthenticationType.Pin, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public async Task DisablePinAsync(Guid walletId, CancellationToken ct = default)
    {
        await _storage.RemoveAsync(StorageKeys.PinVault(walletId), ct);
        await SetAuthTypeAsync(walletId, AuthenticationType.Password, ct);
    }

    public async Task<byte[]> AuthenticateWithPinAsync(Guid walletId, string pin, CancellationToken ct = default)
    {
        EncryptedVault pinVault = await GetJsonAsync<EncryptedVault>(StorageKeys.PinVault(walletId), ct)
            ?? throw new InvalidOperationException("PIN not enabled");

        return VaultEncryption.Decrypt(pinVault, pin);
    }

    public async Task ChangePinAsync(Guid walletId, string oldPin, string newPin, CancellationToken ct = default)
    {
        byte[] passwordBytes = await AuthenticateWithPinAsync(walletId, oldPin, ct);
        try
        {
            EncryptedVault newPinVault = VaultEncryption.Encrypt(walletId, passwordBytes, newPin, VaultPurpose.PinProtectedPassword);
            await SetJsonAsync(StorageKeys.PinVault(walletId), newPinVault, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    #endregion

    #region Custom Provider Config

    public async Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        string key = GetCustomConfigKey(chainInfo);
        return configs.TryGetValue(key, out CustomProviderConfig? config) ? config : null;
    }

    public async Task SaveCustomProviderConfigAsync(CustomProviderConfig config, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        string key = GetCustomConfigKey(config.Chain, config.Network);
        configs[key] = config;
        await SetJsonAsync(StorageKeys.CustomConfigs, configs, ct);
    }

    public async Task DeleteCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        string key = GetCustomConfigKey(chainInfo);
        if (configs.Remove(key))
            await SetJsonAsync(StorageKeys.CustomConfigs, configs, ct);

        await DeleteCustomApiKeyAsync(chainInfo, ct);
    }

    public async Task SaveCustomApiKeyAsync(ChainInfo chainInfo, string apiKey, string password, CancellationToken ct = default)
    {
        byte[] apiKeyBytes = Encoding.UTF8.GetBytes(apiKey);
        try
        {
            EncryptedVault vault = VaultEncryption.Encrypt(Guid.Empty, apiKeyBytes, password);
            string key = StorageKeys.ApiKeyVault((int)chainInfo.Chain, (int)chainInfo.Network);
            await SetJsonAsync(key, vault, ct);

            // Update config to indicate API key exists
            CustomProviderConfig? config = await GetCustomProviderConfigAsync(chainInfo, ct);
            if (config != null && !config.HasCustomApiKey)
            {
                await SaveCustomProviderConfigAsync(config with { HasCustomApiKey = true }, ct);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(apiKeyBytes);
        }
    }

    public async Task<byte[]?> UnlockCustomApiKeyAsync(ChainInfo chainInfo, string password, CancellationToken ct = default)
    {
        string key = StorageKeys.ApiKeyVault((int)chainInfo.Chain, (int)chainInfo.Network);
        EncryptedVault? vault = await GetJsonAsync<EncryptedVault>(key, ct);
        if (vault is null)
            return null;

        return VaultEncryption.Decrypt(vault, password);
    }

    public async Task DeleteCustomApiKeyAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        string key = StorageKeys.ApiKeyVault((int)chainInfo.Chain, (int)chainInfo.Network);
        await _storage.RemoveAsync(key, ct);

        // Update config to indicate API key no longer exists
        CustomProviderConfig? config = await GetCustomProviderConfigAsync(chainInfo, ct);
        if (config != null && config.HasCustomApiKey)
        {
            await SaveCustomProviderConfigAsync(config with { HasCustomApiKey = false }, ct);
        }
    }

    private static string GetCustomConfigKey(ChainInfo chainInfo)
        => $"{(int)chainInfo.Chain}:{(int)chainInfo.Network}";

    private static string GetCustomConfigKey(ChainType chain, NetworkType network)
        => $"{(int)chain}:{(int)network}";

    #endregion

    #region Data Service Config (Not Yet Implemented)

    public Task<DataServiceConfig?> GetDataServiceConfigAsync(DataServiceType serviceType, CancellationToken ct = default)
        => Task.FromResult<DataServiceConfig?>(null);

    public Task<IReadOnlyList<DataServiceConfig>> GetAllDataServiceConfigsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DataServiceConfig>>([]);

    public Task SaveDataServiceConfigAsync(DataServiceConfig config, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task DeleteDataServiceConfigAsync(DataServiceType serviceType, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SaveDataServiceApiKeyAsync(DataServiceType serviceType, string apiKey, string password, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<byte[]?> UnlockDataServiceApiKeyAsync(DataServiceType serviceType, string password, CancellationToken ct = default)
        => Task.FromResult<byte[]?>(null);

    public Task DeleteDataServiceApiKeyAsync(DataServiceType serviceType, CancellationToken ct = default)
        => Task.CompletedTask;

    #endregion

    #region JSON Helpers

    private async Task<T?> GetJsonAsync<T>(string key, CancellationToken ct = default)
    {
        string? json = await _storage.GetAsync(key, ct);
        if (string.IsNullOrEmpty(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }

    private async Task SetJsonAsync<T>(string key, T value, CancellationToken ct = default)
    {
        string json = JsonSerializer.Serialize(value);
        await _storage.SetAsync(key, json, ct);
    }

    #endregion
}
