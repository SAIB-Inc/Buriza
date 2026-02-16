using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buriza.Core.Crypto;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;
using Buriza.Core.Models.Wallet;
using Buriza.Core.Storage;

namespace Buriza.Tests.Mocks;

/// <summary>
/// Test storage service using VaultEncryption only.
/// PIN/device-auth are not supported.
/// </summary>
public sealed class TestWalletStorageService(IStorageProvider storage) : BurizaStorageBase
{
    private readonly IStorageProvider _storage = storage;
    private const int MaxFailedAttemptsBeforeLockout = 5;
    private static readonly TimeSpan BaseLockoutDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxLockoutDuration = TimeSpan.FromHours(1);

    public override Task<string?> GetAsync(string key, CancellationToken ct = default)
        => _storage.GetAsync(key, ct);

    public override Task SetAsync(string key, string value, CancellationToken ct = default)
        => _storage.SetAsync(key, value, ct);

    public override Task RemoveAsync(string key, CancellationToken ct = default)
        => _storage.RemoveAsync(key, ct);

    public override Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => _storage.ExistsAsync(key, ct);

    public override Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default)
        => _storage.GetKeysAsync(prefix, ct);

    public override Task ClearAsync(CancellationToken ct = default)
        => _storage.ClearAsync(ct);

    private Task<string?> GetSecureAsync(string key, CancellationToken ct = default)
        => _storage.GetAsync(key, ct);

    private Task SetSecureAsync(string key, string value, CancellationToken ct = default)
        => _storage.SetAsync(key, value, ct);

    private Task RemoveSecureAsync(string key, CancellationToken ct = default)
        => _storage.RemoveAsync(key, ct);

    public override Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
        => Task.FromResult(new DeviceCapabilities(
            IsSupported: false,
            SupportsBiometrics: false,
            SupportsPin: false,
            AvailableTypes: []));

    public override Task<AuthenticationType> GetAuthTypeAsync(Guid walletId, CancellationToken ct = default)
        => Task.FromResult(AuthenticationType.Password);

    public override Task SetAuthTypeAsync(Guid walletId, AuthenticationType type, CancellationToken ct = default)
        => Task.CompletedTask;

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
        string? value = await _storage.GetAsync(StorageKeys.ActiveWallet, ct);
        return string.IsNullOrEmpty(value) ? null : Guid.TryParse(value, out Guid id) ? id : null;
    }

    public override Task SetActiveWalletIdAsync(Guid walletId, CancellationToken ct = default)
        => _storage.SetAsync(StorageKeys.ActiveWallet, walletId.ToString("N"), ct);

    public override Task ClearActiveWalletIdAsync(CancellationToken ct = default)
        => _storage.RemoveAsync(StorageKeys.ActiveWallet, ct);

    public override async Task<bool> HasVaultAsync(Guid walletId, CancellationToken ct = default)
    {
        string? json = await _storage.GetAsync(StorageKeys.Vault(walletId), ct);
        return !string.IsNullOrEmpty(json);
    }

        public override async Task CreateVaultAsync(Guid walletId, byte[] mnemonic, ReadOnlyMemory<byte> passwordBytes, CancellationToken ct = default)
    {
        EncryptedVault vault = VaultEncryption.Encrypt(walletId, mnemonic, passwordBytes.Span);
        await SetJsonAsync(StorageKeys.Vault(walletId), vault, ct);
    }

    public override async Task<byte[]> UnlockVaultAsync(Guid walletId, string? passwordOrPin, string? biometricReason = null, CancellationToken ct = default)
    {
        await EnsureNotLockedAsync(walletId, ct);

        string password = passwordOrPin ?? throw new ArgumentException("Password required", nameof(passwordOrPin));
        try
        {
            EncryptedVault vault = await GetJsonAsync<EncryptedVault>(StorageKeys.Vault(walletId), ct)
                ?? throw new InvalidOperationException("Vault not found");
            byte[] mnemonic = VaultEncryption.Decrypt(vault, password);
            await ResetLockoutStateAsync(walletId, ct);
            return mnemonic;
        }
        catch (CryptographicException)
        {
            await RegisterFailedAttemptAsync(walletId, ct);
            throw;
        }
    }

    public override async Task<bool> VerifyPasswordAsync(Guid walletId, string password, CancellationToken ct = default)
    {
        await EnsureNotLockedAsync(walletId, ct);

        EncryptedVault? vault = await GetJsonAsync<EncryptedVault>(StorageKeys.Vault(walletId), ct);
        if (vault is null) return false;

        bool ok = VaultEncryption.VerifyPassword(vault, password);
        if (ok)
            await ResetLockoutStateAsync(walletId, ct);
        else
            await RegisterFailedAttemptAsync(walletId, ct);
        return ok;
    }

    public override async Task ChangePasswordAsync(Guid walletId, string oldPassword, string newPassword, CancellationToken ct = default)
    {
        await EnsureNotLockedAsync(walletId, ct);

        byte[] mnemonicBytes;
        try
        {
            EncryptedVault vault = await GetJsonAsync<EncryptedVault>(StorageKeys.Vault(walletId), ct)
                ?? throw new InvalidOperationException("Vault not found");
            mnemonicBytes = VaultEncryption.Decrypt(vault, oldPassword);
        }
        catch (CryptographicException)
        {
            await RegisterFailedAttemptAsync(walletId, ct);
            throw;
        }

        try
        {
            byte[] newPasswordBytes = Encoding.UTF8.GetBytes(newPassword);
            try
            {
                await CreateVaultAsync(walletId, mnemonicBytes, newPasswordBytes, ct);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(newPasswordBytes);
            }
            await ResetLockoutStateAsync(walletId, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    public override async Task DeleteVaultAsync(Guid walletId, CancellationToken ct = default)
    {
        await _storage.RemoveAsync(StorageKeys.Vault(walletId), ct);
        await _storage.RemoveAsync(StorageKeys.PinVault(walletId), ct);
        await _storage.RemoveAsync(StorageKeys.AuthType(walletId), ct);
        await _storage.RemoveAsync(StorageKeys.AuthTypeHmac(walletId), ct);
        await _storage.RemoveAsync(StorageKeys.LockoutState(walletId), ct);
    }

    public override Task<bool> IsDeviceAuthEnabledAsync(Guid walletId, CancellationToken ct = default)
        => Task.FromResult(false);

    public override Task EnableDeviceAuthAsync(Guid walletId, string password, CancellationToken ct = default)
        => throw new NotSupportedException("Device auth is not supported in tests.");

    public override Task DisableDeviceAuthAsync(Guid walletId, CancellationToken ct = default)
        => Task.CompletedTask;

    public override Task<byte[]> AuthenticateWithDeviceAuthAsync(Guid walletId, string reason, CancellationToken ct = default)
        => throw new NotSupportedException("Device auth is not supported in tests.");

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
            try
            {
                ChainInfo info = ChainRegistry.Get(config.Chain, config.Network);
                Guid vaultId = DeriveApiKeyVaultId(info);
                EncryptedVault vault = VaultEncryption.Encrypt(vaultId, apiKeyBytes, password, VaultPurpose.ApiKey);
                string vaultKey = StorageKeys.ApiKeyVault((int)config.Chain, (int)config.Network);
                await SetJsonAsync(vaultKey, vault, ct);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(apiKeyBytes);
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

        byte[] apiKeyBytes = VaultEncryption.Decrypt(vault, password);
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

    private async Task EnsureNotLockedAsync(Guid walletId, CancellationToken ct)
    {
        LockoutState? state = await TryGetLockoutStateAsync(walletId, ct);
        if (state?.LockoutEndUtc is null) return;

        if (state.LockoutEndUtc > DateTime.UtcNow)
            throw new InvalidOperationException($"Wallet locked until {state.LockoutEndUtc:O}");

        await ResetLockoutStateAsync(walletId, ct);
    }

    private async Task RegisterFailedAttemptAsync(Guid walletId, CancellationToken ct)
    {
        LockoutState? state = await TryGetLockoutStateAsync(walletId, ct);
        int failedAttempts = (state?.FailedAttempts ?? 0) + 1;
        DateTime? lockoutEndUtc = null;

        if (failedAttempts >= MaxFailedAttemptsBeforeLockout)
        {
            int exponent = failedAttempts - MaxFailedAttemptsBeforeLockout;
            double scale = Math.Pow(2, Math.Min(exponent, 6));
            TimeSpan duration = TimeSpan.FromSeconds(BaseLockoutDuration.TotalSeconds * scale);
            if (duration > MaxLockoutDuration)
                duration = MaxLockoutDuration;

            lockoutEndUtc = DateTime.UtcNow.Add(duration);
        }

        await SaveLockoutStateAsync(walletId, failedAttempts, lockoutEndUtc, ct);
    }

    private Task ResetLockoutStateAsync(Guid walletId, CancellationToken ct)
        => _storage.RemoveAsync(StorageKeys.LockoutState(walletId), ct);

    private async Task<LockoutState?> TryGetLockoutStateAsync(Guid walletId, CancellationToken ct)
    {
        string? json = await _storage.GetAsync(StorageKeys.LockoutState(walletId), ct);
        if (string.IsNullOrEmpty(json)) return null;

        LockoutState? state;
        try
        {
            state = JsonSerializer.Deserialize<LockoutState>(json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (state is null || string.IsNullOrEmpty(state.Hmac))
        {
            await ResetLockoutStateAsync(walletId, ct);
            return null;
        }

        byte[] hmacKey = await GetOrCreateLockoutKeyAsync(ct);
        string expected = ComputeLockoutHmac(hmacKey, state.FailedAttempts, state.LockoutEndUtc, state.UpdatedAtUtc);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(state.Hmac), Convert.FromBase64String(expected)))
            {
                await ResetLockoutStateAsync(walletId, ct);
                return null;
            }
        }
        catch (FormatException)
        {
            await ResetLockoutStateAsync(walletId, ct);
            return null;
        }

        return state;
    }

    private async Task SaveLockoutStateAsync(Guid walletId, int failedAttempts, DateTime? lockoutEndUtc, CancellationToken ct)
    {
        DateTime updatedAtUtc = DateTime.UtcNow;
        byte[] hmacKey = await GetOrCreateLockoutKeyAsync(ct);
        string hmac = ComputeLockoutHmac(hmacKey, failedAttempts, lockoutEndUtc, updatedAtUtc);

        LockoutState state = new()
        {
            FailedAttempts = failedAttempts,
            LockoutEndUtc = lockoutEndUtc,
            UpdatedAtUtc = updatedAtUtc,
            Hmac = hmac
        };

        string json = JsonSerializer.Serialize(state);
        await _storage.SetAsync(StorageKeys.LockoutState(walletId), json, ct);
    }

    private async Task<byte[]> GetOrCreateLockoutKeyAsync(CancellationToken ct)
    {
        string? keyBase64 = await _storage.GetAsync(StorageKeys.LockoutKey, ct);
        if (string.IsNullOrEmpty(keyBase64))
        {
            byte[] key = RandomNumberGenerator.GetBytes(32);
            await _storage.SetAsync(StorageKeys.LockoutKey, Convert.ToBase64String(key), ct);
            return key;
        }

        return Convert.FromBase64String(keyBase64);
    }
}
