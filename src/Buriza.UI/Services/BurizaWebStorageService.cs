using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buriza.Core.Crypto;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;
using Buriza.Core.Models.Wallet;
using Buriza.Core.Storage;
using Microsoft.JSInterop;

namespace Buriza.UI.Services;

/// <summary>
/// Web/Extension storage service using VaultEncryption only.
/// PIN/device-auth are not supported.
/// </summary>
public sealed class BurizaWebStorageService(IJSRuntime js) : BurizaStorageBase
{
    private readonly IJSRuntime _js = js;

    #region IStorageProvider

    public override async Task<string?> GetAsync(string key, CancellationToken ct = default)
        => await _js.InvokeAsync<string?>("buriza.storage.get", ct, key);

    public override async Task SetAsync(string key, string value, CancellationToken ct = default)
        => await _js.InvokeVoidAsync("buriza.storage.set", ct, key, value);

    public override async Task RemoveAsync(string key, CancellationToken ct = default)
        => await _js.InvokeVoidAsync("buriza.storage.remove", ct, key);

    public override async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => await _js.InvokeAsync<bool>("buriza.storage.exists", ct, key);

    public override async Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default)
        => await _js.InvokeAsync<string[]>("buriza.storage.getKeys", ct, prefix) ?? [];

    public override async Task ClearAsync(CancellationToken ct = default)
        => await _js.InvokeVoidAsync("buriza.storage.clear", ct);

    #endregion

    #region Capabilities

    public override Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
        => Task.FromResult(new DeviceCapabilities(
            IsSupported: false,
            SupportsBiometrics: false,
            SupportsPin: false,
            AvailableTypes: []));

    #endregion

    #region Auth Type

    public override Task<AuthenticationType> GetAuthTypeAsync(Guid walletId, CancellationToken ct = default)
        => Task.FromResult(AuthenticationType.Password);

    public override Task SetAuthTypeAsync(Guid walletId, AuthenticationType type, CancellationToken ct = default)
        => Task.CompletedTask;

    #endregion

    #region Wallet Metadata

    public override async Task SaveWalletAsync(BurizaWallet wallet, CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = [.. await LoadAllWalletsAsync(ct)];
        int existingIndex = wallets.FindIndex(w => w.Id == wallet.Id);

        if (existingIndex >= 0)
            wallets[existingIndex] = wallet;
        else
            wallets.Add(wallet);

        await SetJsonAsync(StorageKeys.Wallets, wallets, ct);
    }

    public override async Task<BurizaWallet?> LoadWalletAsync(Guid walletId, CancellationToken ct = default)
    {
        IReadOnlyList<BurizaWallet> wallets = await LoadAllWalletsAsync(ct);
        return wallets.FirstOrDefault(w => w.Id == walletId);
    }

    public override async Task<IReadOnlyList<BurizaWallet>> LoadAllWalletsAsync(CancellationToken ct = default)
        => await GetJsonAsync<List<BurizaWallet>>(StorageKeys.Wallets, ct) ?? [];

    public override async Task DeleteWalletAsync(Guid walletId, CancellationToken ct = default)
    {
        List<BurizaWallet> wallets = [.. await LoadAllWalletsAsync(ct)];
        wallets.RemoveAll(w => w.Id == walletId);
        await SetJsonAsync(StorageKeys.Wallets, wallets, ct);

        await DeleteVaultAsync(walletId, ct);
    }

    public override async Task<Guid?> GetActiveWalletIdAsync(CancellationToken ct = default)
    {
        string? value = await GetAsync(StorageKeys.ActiveWallet, ct);
        return string.IsNullOrEmpty(value) ? null : Guid.TryParse(value, out Guid id) ? id : null;
    }

    public override Task SetActiveWalletIdAsync(Guid walletId, CancellationToken ct = default)
        => SetAsync(StorageKeys.ActiveWallet, walletId.ToString("N"), ct);

    public override Task ClearActiveWalletIdAsync(CancellationToken ct = default)
        => RemoveAsync(StorageKeys.ActiveWallet, ct);

    #endregion

    #region Vault Operations

    public override async Task<bool> HasVaultAsync(Guid walletId, CancellationToken ct = default)
    {
        string? json = await GetAsync(StorageKeys.Vault(walletId), ct);
        return !string.IsNullOrEmpty(json);
    }

        public override async Task CreateVaultAsync(Guid walletId, byte[] mnemonic, ReadOnlyMemory<byte> passwordBytes, CancellationToken ct = default)
    {
        EncryptedVault vault = VaultEncryption.Encrypt(walletId, mnemonic, passwordBytes.Span);
        await SetJsonAsync(StorageKeys.Vault(walletId), vault, ct);
    }

    public override async Task<byte[]> UnlockVaultAsync(Guid walletId, string? passwordOrPin, string? biometricReason = null, CancellationToken ct = default)
    {
        string password = passwordOrPin ?? throw new ArgumentException("Password required", nameof(passwordOrPin));
        EncryptedVault vault = await GetJsonAsync<EncryptedVault>(StorageKeys.Vault(walletId), ct)
            ?? throw new InvalidOperationException("Vault not found");
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            return VaultEncryption.Decrypt(vault, passwordBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public override async Task<bool> VerifyPasswordAsync(Guid walletId, string password, CancellationToken ct = default)
    {
        EncryptedVault? vault = await GetJsonAsync<EncryptedVault>(StorageKeys.Vault(walletId), ct);
        if (vault is null) return false;

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            return VaultEncryption.VerifyPassword(vault, passwordBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public override async Task ChangePasswordAsync(Guid walletId, string oldPassword, string newPassword, CancellationToken ct = default)
    {
        byte[] oldPasswordBytes = Encoding.UTF8.GetBytes(oldPassword);
        byte[] newPasswordBytes = Encoding.UTF8.GetBytes(newPassword);
        byte[]? mnemonicBytes = null;
        try
        {
            EncryptedVault vault = await GetJsonAsync<EncryptedVault>(StorageKeys.Vault(walletId), ct)
                ?? throw new InvalidOperationException("Vault not found");
            mnemonicBytes = VaultEncryption.Decrypt(vault, oldPasswordBytes);
            await CreateVaultAsync(walletId, mnemonicBytes, newPasswordBytes, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(oldPasswordBytes);
            CryptographicOperations.ZeroMemory(newPasswordBytes);
            if (mnemonicBytes is not null)
                CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    public override async Task DeleteVaultAsync(Guid walletId, CancellationToken ct = default)
    {
        await RemoveAsync(StorageKeys.Vault(walletId), ct);
        await RemoveAsync(StorageKeys.PinVault(walletId), ct);
        await RemoveAsync(StorageKeys.AuthType(walletId), ct);
        await RemoveAsync(StorageKeys.AuthTypeHmac(walletId), ct);
        await RemoveAsync(StorageKeys.LockoutState(walletId), ct);
    }

    #endregion

    #region Unsupported Auth

    public override Task<bool> IsDeviceAuthEnabledAsync(Guid walletId, CancellationToken ct = default)
        => Task.FromResult(false);

    public override Task EnableDeviceAuthAsync(Guid walletId, string password, CancellationToken ct = default)
        => throw new NotSupportedException("Device auth is not supported on web/extension.");

    public override Task DisableDeviceAuthAsync(Guid walletId, CancellationToken ct = default)
        => Task.CompletedTask;

    public override Task<byte[]> AuthenticateWithDeviceAuthAsync(Guid walletId, string reason, CancellationToken ct = default)
        => throw new NotSupportedException("Device auth is not supported on web/extension.");

    #endregion

    #region Custom Provider Config

    public override async Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        string key = GetCustomConfigKey(chainInfo);
        return configs.TryGetValue(key, out CustomProviderConfig? config) ? config : null;
    }

    public override async Task SaveCustomProviderConfigAsync(CustomProviderConfig config, string? apiKey, string password, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        string key = GetCustomConfigKey(config.Chain, config.Network);
        CustomProviderConfig updated = config with { HasCustomApiKey = !string.IsNullOrEmpty(apiKey) };
        configs[key] = updated;
        await SetJsonAsync(StorageKeys.CustomConfigs, configs, ct);

        if (!string.IsNullOrEmpty(apiKey))
        {
            byte[] apiKeyBytes = Encoding.UTF8.GetBytes(apiKey);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            try
            {
                ChainInfo info = ChainRegistry.Get(config.Chain, config.Network);
                Guid vaultId = DeriveApiKeyVaultId(info);
                EncryptedVault vault = VaultEncryption.Encrypt(vaultId, apiKeyBytes, passwordBytes, VaultPurpose.ApiKey);
                string vaultKey = StorageKeys.ApiKeyVault((int)config.Chain, (int)config.Network);
                await SetJsonAsync(vaultKey, vault, ct);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(apiKeyBytes);
                CryptographicOperations.ZeroMemory(passwordBytes);
            }
        }
        else
        {
            await RemoveAsync(StorageKeys.ApiKeyVault((int)config.Chain, (int)config.Network), ct);
        }
    }

    public override async Task DeleteCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        Dictionary<string, CustomProviderConfig> configs = await GetJsonAsync<Dictionary<string, CustomProviderConfig>>(StorageKeys.CustomConfigs, ct) ?? [];
        string key = GetCustomConfigKey(chainInfo);
        if (configs.Remove(key))
            await SetJsonAsync(StorageKeys.CustomConfigs, configs, ct);

        await RemoveAsync(StorageKeys.ApiKeyVault((int)chainInfo.Chain, (int)chainInfo.Network), ct);
    }

    public override async Task<(CustomProviderConfig Config, string? ApiKey)?> GetCustomProviderConfigWithApiKeyAsync(
        ChainInfo chainInfo,
        string password,
        CancellationToken ct = default)
    {
        CustomProviderConfig? config = await GetCustomProviderConfigAsync(chainInfo, ct);
        if (config is null)
            return null;

        if (!config.HasCustomApiKey)
            return (config, null);

        string key = StorageKeys.ApiKeyVault((int)chainInfo.Chain, (int)chainInfo.Network);
        EncryptedVault? vault = await GetJsonAsync<EncryptedVault>(key, ct);
        if (vault is null)
            return (config, null);

        Guid expectedId = DeriveApiKeyVaultId(chainInfo);
        if (vault.Purpose != VaultPurpose.ApiKey || vault.WalletId != expectedId)
            throw new CryptographicException("Invalid API key vault metadata");

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] apiKeyBytes;
        try
        {
            apiKeyBytes = VaultEncryption.Decrypt(vault, passwordBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
        try
        {
            string apiKey = Encoding.UTF8.GetString(apiKeyBytes);
            return (config, apiKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(apiKeyBytes);
        }
    }

    #endregion

}
