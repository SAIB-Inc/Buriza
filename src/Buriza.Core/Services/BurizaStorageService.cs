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
    IPlatformSecureStorage? secureStorage,
    IBiometricService biometricService,
    BurizaStorageOptions? storageOptions = null) : IStorageProvider, ISecureStorageProvider
{
    private readonly IPlatformStorage _storage = platformStorage;
    private readonly IPlatformSecureStorage? _secureStorage = secureStorage;
    private readonly IBiometricService _biometricService = biometricService;
    private readonly BurizaStorageOptions _storageOptions = storageOptions ?? new BurizaStorageOptions();
    private const int MinPinLength = 6;
    private const int MaxFailedAttemptsBeforeLockout = 5;
    private static readonly TimeSpan BaseLockoutDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxLockoutDuration = TimeSpan.FromHours(1);
    private bool UseDirectSeedStorage => _storageOptions.Mode == StorageMode.DirectSecure;

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

    // ISecureStorageProvider remains a compatibility facade over IPlatformStorage.
    // Direct seed storage uses IPlatformSecureStorage instead (MAUI SecureStorage, etc.).
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
        return await _biometricService.GetCapabilitiesAsync(ct);
    }

    #endregion

    #region Auth Type Management

    public async Task<AuthenticationType> GetAuthTypeAsync(Guid walletId, CancellationToken ct = default)
    {
        string? typeStr = await _storage.GetAsync(StorageKeys.AuthType(walletId), ct);
        if (string.IsNullOrEmpty(typeStr))
            return AuthenticationType.Password;

        string? hmac = await _storage.GetAsync(StorageKeys.AuthTypeHmac(walletId), ct);
        if (string.IsNullOrEmpty(hmac))
        {
            await _storage.RemoveAsync(StorageKeys.AuthType(walletId), ct);
            await _storage.RemoveAsync(StorageKeys.AuthTypeHmac(walletId), ct);
            return AuthenticationType.Password;
        }

        byte[] key = await GetOrCreateAuthTypeKeyAsync(ct);
        string expected = ComputeAuthTypeHmac(key, walletId, typeStr);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(hmac), Convert.FromBase64String(expected)))
            {
                await _storage.RemoveAsync(StorageKeys.AuthType(walletId), ct);
                await _storage.RemoveAsync(StorageKeys.AuthTypeHmac(walletId), ct);
                return AuthenticationType.Password;
            }
        }
        catch (FormatException)
        {
            await _storage.RemoveAsync(StorageKeys.AuthType(walletId), ct);
            await _storage.RemoveAsync(StorageKeys.AuthTypeHmac(walletId), ct);
            return AuthenticationType.Password;
        }

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
        byte[] key = await GetOrCreateAuthTypeKeyAsync(ct);
        string hmac = ComputeAuthTypeHmac(key, walletId, typeStr);
        await _storage.SetAsync(StorageKeys.AuthTypeHmac(walletId), hmac, ct);
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
        ValidateStorageMode();
        if (UseDirectSeedStorage)
            return await _secureStorage!.ExistsAsync(StorageKeys.SecureSeed(walletId), ct);

        string? json = await _storage.GetAsync(StorageKeys.Vault(walletId), ct);
        return !string.IsNullOrEmpty(json);
    }

    public async Task CreateVaultAsync(Guid walletId, byte[] mnemonic, string password, CancellationToken ct = default)
    {
        ValidateStorageMode();
        if (UseDirectSeedStorage)
        {
            string seed = Convert.ToBase64String(mnemonic);
            await _secureStorage!.SetAsync(StorageKeys.SecureSeed(walletId), seed, ct);
            SecretVerifier verifier = SecretVerifier.Create(password);
            await SetSecureJsonAsync(StorageKeys.PasswordVerifier(walletId), verifier, ct);
            return;
        }

        EncryptedVault vault = VaultEncryption.Encrypt(walletId, mnemonic, password);
        await SetJsonAsync(StorageKeys.Vault(walletId), vault, ct);
    }

    private async Task<byte[]> UnlockVaultWithPasswordAsync(Guid walletId, string password, CancellationToken ct = default)
    {
        ValidateStorageMode();
        if (UseDirectSeedStorage)
        {
            SecretVerifier verifier = await GetSecureJsonAsync<SecretVerifier>(StorageKeys.PasswordVerifier(walletId), ct)
                ?? throw new InvalidOperationException("Password verifier not found");
            if (!verifier.Verify(password))
                throw new CryptographicException("Invalid password");

            string? seed = await _secureStorage!.GetAsync(StorageKeys.SecureSeed(walletId), ct);
            if (string.IsNullOrEmpty(seed))
                throw new InvalidOperationException("Secure seed not found");

            return Convert.FromBase64String(seed);
        }

        EncryptedVault vault = await GetJsonAsync<EncryptedVault>(StorageKeys.Vault(walletId), ct)
            ?? throw new InvalidOperationException("Vault not found");

        return VaultEncryption.Decrypt(vault, password);
    }

    /// <summary>
    /// Unlocks vault based on the configured authentication type.
    /// If Password: uses provided passwordOrPin. If Pin: uses provided passwordOrPin.
    /// If Biometric: uses biometricReason prompt.
    /// </summary>
    public async Task<byte[]> UnlockVaultAsync(Guid walletId, string? passwordOrPin, string? biometricReason = null, CancellationToken ct = default)
    {
        ValidateStorageMode();
        await EnsureNotLockedAsync(walletId, ct);

        AuthenticationType authType = await GetAuthTypeAsync(walletId, ct);

        try
        {
            switch (authType)
            {
                case AuthenticationType.Biometric:
                {
                    byte[] biometricPayload = await AuthenticateWithBiometricAsync(
                        walletId,
                        biometricReason ?? "Unlock your wallet",
                        ct);
                    try
                    {
                        if (UseDirectSeedStorage)
                        {
                            await ResetLockoutStateAsync(walletId, ct);
                            return biometricPayload;
                        }

                        EncryptedVault biometricVault = await GetJsonAsync<EncryptedVault>(StorageKeys.BiometricSeedVault(walletId), ct)
                            ?? throw new InvalidOperationException("Biometric seed vault not found");
                        byte[] mnemonic = VaultEncryption.Decrypt(biometricVault, biometricPayload);
                        await ResetLockoutStateAsync(walletId, ct);
                        return mnemonic;
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(biometricPayload);
                    }
                }
                case AuthenticationType.Pin:
                {
                    string pin = passwordOrPin ?? throw new ArgumentException("PIN required", nameof(passwordOrPin));
                    if (!IsValidPin(pin))
                    {
                        await RegisterFailedAttemptAsync(walletId, ct);
                        throw new ArgumentException("PIN must be at least 6 digits.", nameof(passwordOrPin));
                    }

                    if (UseDirectSeedStorage)
                    {
                        byte[] mnemonic = await AuthenticateWithPinAsync(walletId, pin, ct);
                        await ResetLockoutStateAsync(walletId, ct);
                        return mnemonic;
                    }

                    byte[] passwordBytes = await AuthenticateWithPinAsync(walletId, pin, ct);
                    byte[] vaultMnemonic = await UnlockWithPasswordBytesAsync(passwordBytes, walletId, ct);
                    await ResetLockoutStateAsync(walletId, ct);
                    return vaultMnemonic;
                }
                default:
                {
                    string password = passwordOrPin ?? throw new ArgumentException("Password required", nameof(passwordOrPin));
                    byte[] mnemonic = await UnlockVaultWithPasswordAsync(walletId, password, ct);
                    await ResetLockoutStateAsync(walletId, ct);
                    return mnemonic;
                }
            }
        }
        catch (CryptographicException)
        {
            await RegisterFailedAttemptAsync(walletId, ct);
            throw;
        }
    }

    /// <summary>
    /// Unlocks the password-based vault using provided password bytes.
    /// </summary>
    private async Task<byte[]> UnlockWithPasswordBytesAsync(byte[] passwordBytes, Guid walletId, CancellationToken ct)
    {
        try
        {
            EncryptedVault vault = await GetJsonAsync<EncryptedVault>(StorageKeys.Vault(walletId), ct)
                ?? throw new InvalidOperationException("Vault not found");
            return VaultEncryption.Decrypt(vault, passwordBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public async Task<bool> VerifyPasswordAsync(Guid walletId, string password, CancellationToken ct = default)
    {
        ValidateStorageMode();
        await EnsureNotLockedAsync(walletId, ct);

        if (UseDirectSeedStorage)
        {
            SecretVerifier? verifier = await GetSecureJsonAsync<SecretVerifier>(StorageKeys.PasswordVerifier(walletId), ct);
            if (verifier is null) return false;
            bool okVerifier = verifier.Verify(password);
            if (okVerifier)
                await ResetLockoutStateAsync(walletId, ct);
            else
                await RegisterFailedAttemptAsync(walletId, ct);
            return okVerifier;
        }

        EncryptedVault? vault = await GetJsonAsync<EncryptedVault>(StorageKeys.Vault(walletId), ct);
        if (vault is null) return false;

        bool okVault = VaultEncryption.VerifyPassword(vault, password);
        if (okVault)
            await ResetLockoutStateAsync(walletId, ct);
        else
            await RegisterFailedAttemptAsync(walletId, ct);
        return okVault;
    }

    public async Task ChangePasswordAsync(Guid walletId, string oldPassword, string newPassword, CancellationToken ct = default)
    {
        ValidateStorageMode();
        await EnsureNotLockedAsync(walletId, ct);

        if (UseDirectSeedStorage)
        {
            if (!await VerifyPasswordAsync(walletId, oldPassword, ct))
                throw new CryptographicException("Invalid password");

            SecretVerifier verifier = SecretVerifier.Create(newPassword);
            await SetSecureJsonAsync(StorageKeys.PasswordVerifier(walletId), verifier, ct);
            await ResetLockoutStateAsync(walletId, ct);
            return;
        }

        byte[] mnemonicBytes;
        try
        {
            mnemonicBytes = await UnlockVaultWithPasswordAsync(walletId, oldPassword, ct);
        }
        catch (CryptographicException)
        {
            await RegisterFailedAttemptAsync(walletId, ct);
            throw;
        }
        try
        {
            await CreateVaultAsync(walletId, mnemonicBytes, newPassword, ct);

            if (!UseDirectSeedStorage)
            {
                // PIN/biometric store the old password in web/extension mode.
                await _storage.RemoveAsync(StorageKeys.PinVault(walletId), ct);
                await _biometricService.RemoveSecureAsync(StorageKeys.BiometricKey(walletId), ct);
                await SetAuthTypeAsync(walletId, AuthenticationType.Password, ct);
            }

            await ResetLockoutStateAsync(walletId, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    public async Task DeleteVaultAsync(Guid walletId, CancellationToken ct = default)
    {
        ValidateStorageMode();
        await _storage.RemoveAsync(StorageKeys.Vault(walletId), ct);
        await _storage.RemoveAsync(StorageKeys.PinVault(walletId), ct);
        await _storage.RemoveAsync(StorageKeys.BiometricSeedVault(walletId), ct);
        await _secureStorage!.RemoveAsync(StorageKeys.PasswordVerifier(walletId), ct);
        await _secureStorage!.RemoveAsync(StorageKeys.PinVerifier(walletId), ct);
        await _secureStorage!.RemoveAsync(StorageKeys.SecureSeed(walletId), ct);
        await _biometricService.RemoveSecureAsync(StorageKeys.BiometricSeed(walletId), ct);
        await _biometricService.RemoveSecureAsync(StorageKeys.BiometricKey(walletId), ct);
        await _storage.RemoveAsync(StorageKeys.AuthType(walletId), ct);
        await _storage.RemoveAsync(StorageKeys.AuthTypeHmac(walletId), ct);
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
        ValidateStorageMode();
        if (!await VerifyPasswordAsync(walletId, password, ct))
            throw new CryptographicException("Invalid password");

        byte[] mnemonicBytes = await UnlockVaultWithPasswordAsync(walletId, password, ct);
        try
        {
            if (UseDirectSeedStorage)
            {
                await _biometricService.StoreSecureAsync(StorageKeys.BiometricSeed(walletId), mnemonicBytes, ct);
            }
            else
            {
                byte[] biometricKey = RandomNumberGenerator.GetBytes(32);
                try
                {
                    EncryptedVault biometricSeedVault = VaultEncryption.Encrypt(
                        walletId,
                        mnemonicBytes,
                        biometricKey,
                        VaultPurpose.BiometricSeed);

                    await SetJsonAsync(StorageKeys.BiometricSeedVault(walletId), biometricSeedVault, ct);
                    await _biometricService.StoreSecureAsync(StorageKeys.BiometricKey(walletId), biometricKey, ct);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(biometricKey);
                }
            }
            await SetAuthTypeAsync(walletId, AuthenticationType.Biometric, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    public async Task DisableBiometricAsync(Guid walletId, CancellationToken ct = default)
    {
        ValidateStorageMode();
        await _biometricService.RemoveSecureAsync(StorageKeys.BiometricKey(walletId), ct);
        await _biometricService.RemoveSecureAsync(StorageKeys.BiometricSeed(walletId), ct);
        await _storage.RemoveAsync(StorageKeys.BiometricSeedVault(walletId), ct);
        await SetAuthTypeAsync(walletId, AuthenticationType.Password, ct);
    }

    public async Task<byte[]> AuthenticateWithBiometricAsync(Guid walletId, string reason, CancellationToken ct = default)
    {
        ValidateStorageMode();
        if (UseDirectSeedStorage)
        {
            byte[]? seed = await _biometricService.RetrieveSecureAsync(StorageKeys.BiometricSeed(walletId), reason, ct);
            return seed ?? throw new CryptographicException("Biometric authentication failed");
        }

        byte[]? keyBytes = await _biometricService.RetrieveSecureAsync(StorageKeys.BiometricKey(walletId), reason, ct);
        return keyBytes ?? throw new CryptographicException("Biometric authentication failed");
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
        ValidateStorageMode();
        if (!IsValidPin(pin))
            throw new ArgumentException("PIN must be at least 6 digits.", nameof(pin));

        if (!await VerifyPasswordAsync(walletId, password, ct))
            throw new CryptographicException("Invalid password");

        if (UseDirectSeedStorage)
        {
            SecretVerifier pinVerifier = SecretVerifier.Create(pin);
            await SetSecureJsonAsync(StorageKeys.PinVerifier(walletId), pinVerifier, ct);
            await SetAuthTypeAsync(walletId, AuthenticationType.Pin, ct);
        }
        else
        {
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
    }

    public async Task DisablePinAsync(Guid walletId, CancellationToken ct = default)
    {
        ValidateStorageMode();
        await _storage.RemoveAsync(StorageKeys.PinVault(walletId), ct);
        await _secureStorage!.RemoveAsync(StorageKeys.PinVerifier(walletId), ct);
        await SetAuthTypeAsync(walletId, AuthenticationType.Password, ct);
    }

    public async Task<byte[]> AuthenticateWithPinAsync(Guid walletId, string pin, CancellationToken ct = default)
    {
        ValidateStorageMode();
        if (!IsValidPin(pin))
            throw new ArgumentException("PIN must be at least 6 digits.", nameof(pin));

        if (UseDirectSeedStorage)
        {
            SecretVerifier verifier = await GetSecureJsonAsync<SecretVerifier>(StorageKeys.PinVerifier(walletId), ct)
                ?? throw new InvalidOperationException("PIN not enabled");
            if (!verifier.Verify(pin))
                throw new CryptographicException("Invalid PIN");

            string? seed = await _secureStorage!.GetAsync(StorageKeys.SecureSeed(walletId), ct);
            if (string.IsNullOrEmpty(seed))
                throw new InvalidOperationException("Secure seed not found");

            return Convert.FromBase64String(seed);
        }

        EncryptedVault pinVault = await GetJsonAsync<EncryptedVault>(StorageKeys.PinVault(walletId), ct)
            ?? throw new InvalidOperationException("PIN not enabled");

        return VaultEncryption.Decrypt(pinVault, pin);
    }

    public async Task ChangePinAsync(Guid walletId, string oldPin, string newPin, CancellationToken ct = default)
    {
        ValidateStorageMode();
        if (!IsValidPin(newPin))
            throw new ArgumentException("PIN must be at least 6 digits.", nameof(newPin));

        await EnsureNotLockedAsync(walletId, ct);

        if (UseDirectSeedStorage)
        {
            SecretVerifier verifier = await GetSecureJsonAsync<SecretVerifier>(StorageKeys.PinVerifier(walletId), ct)
                ?? throw new InvalidOperationException("PIN not enabled");
            if (!verifier.Verify(oldPin))
            {
                await RegisterFailedAttemptAsync(walletId, ct);
                throw new CryptographicException("Invalid PIN");
            }

            SecretVerifier newVerifier = SecretVerifier.Create(newPin);
            await SetSecureJsonAsync(StorageKeys.PinVerifier(walletId), newVerifier, ct);
            await ResetLockoutStateAsync(walletId, ct);
            return;
        }

        byte[] pinPayload;
        try
        {
            pinPayload = await AuthenticateWithPinAsync(walletId, oldPin, ct);
        }
        catch (CryptographicException)
        {
            await RegisterFailedAttemptAsync(walletId, ct);
            throw;
        }
        try
        {
            VaultPurpose purpose = UseDirectSeedStorage
                ? VaultPurpose.PinProtectedSeed
                : VaultPurpose.PinProtectedPassword;

            if (UseDirectSeedStorage)
            {
                SecretVerifier verifier = SecretVerifier.Create(newPin);
                await SetSecureJsonAsync(StorageKeys.PinVerifier(walletId), verifier, ct);
            }
            else
            {
                EncryptedVault newPinVault = VaultEncryption.Encrypt(walletId, pinPayload, newPin, purpose);
                await SetJsonAsync(StorageKeys.PinVault(walletId), newPinVault, ct);
            }
            await ResetLockoutStateAsync(walletId, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pinPayload);
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
            Guid vaultId = DeriveApiKeyVaultId(chainInfo);
            EncryptedVault vault = VaultEncryption.Encrypt(vaultId, apiKeyBytes, password, VaultPurpose.ApiKey);
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

        Guid expectedId = DeriveApiKeyVaultId(chainInfo);
        if (vault.Purpose != VaultPurpose.ApiKey || vault.WalletId != expectedId)
            throw new CryptographicException("Invalid API key vault metadata");

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

    #region Lockout

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
            return await TamperedLockoutStateAsync(walletId, ct);

        byte[] hmacKey = await GetOrCreateLockoutKeyAsync(ct);
        string expected = ComputeLockoutHmac(hmacKey, state.FailedAttempts, state.LockoutEndUtc, state.UpdatedAtUtc);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(state.Hmac), Convert.FromBase64String(expected)))
            {
                return await TamperedLockoutStateAsync(walletId, ct);
            }
        }
        catch (FormatException)
        {
            return await TamperedLockoutStateAsync(walletId, ct);
        }

        return state;
    }

    private async Task<LockoutState> TamperedLockoutStateAsync(Guid walletId, CancellationToken ct)
    {
        DateTime updatedAtUtc = DateTime.UtcNow;
        DateTime lockoutEndUtc = updatedAtUtc.Add(BaseLockoutDuration);
        byte[] hmacKey = await GetOrCreateLockoutKeyAsync(ct);
        string hmac = ComputeLockoutHmac(hmacKey, MaxFailedAttemptsBeforeLockout, lockoutEndUtc, updatedAtUtc);

        LockoutState state = new()
        {
            FailedAttempts = MaxFailedAttemptsBeforeLockout,
            LockoutEndUtc = lockoutEndUtc,
            UpdatedAtUtc = updatedAtUtc,
            Hmac = hmac
        };

        string json = JsonSerializer.Serialize(state);
        await _storage.SetAsync(StorageKeys.LockoutState(walletId), json, ct);
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
        string? keyBase64 = await _secureStorage!.GetAsync(StorageKeys.LockoutKey, ct);
        if (string.IsNullOrEmpty(keyBase64))
        {
            byte[] key = RandomNumberGenerator.GetBytes(32);
            await _secureStorage!.SetAsync(StorageKeys.LockoutKey, Convert.ToBase64String(key), ct);
            return key;
        }

        return Convert.FromBase64String(keyBase64);
    }

    private async Task<byte[]> GetOrCreateAuthTypeKeyAsync(CancellationToken ct)
    {
        string? keyBase64 = await _secureStorage!.GetAsync(StorageKeys.AuthTypeKey, ct);
        if (string.IsNullOrEmpty(keyBase64))
        {
            byte[] key = RandomNumberGenerator.GetBytes(32);
            await _secureStorage!.SetAsync(StorageKeys.AuthTypeKey, Convert.ToBase64String(key), ct);
            return key;
        }

        return Convert.FromBase64String(keyBase64);
    }

    private static string ComputeAuthTypeHmac(byte[] key, Guid walletId, string typeStr)
    {
        string payload = $"{walletId:N}|{typeStr}";
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        using HMACSHA256 hmac = new(key);
        return Convert.ToBase64String(hmac.ComputeHash(payloadBytes));
    }

    private static string ComputeLockoutHmac(byte[] key, int failedAttempts, DateTime? lockoutEndUtc, DateTime updatedAtUtc)
    {
        string payload = $"{failedAttempts}|{lockoutEndUtc?.Ticks ?? 0}|{updatedAtUtc.Ticks}";
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        using HMACSHA256 hmac = new(key);
        return Convert.ToBase64String(hmac.ComputeHash(payloadBytes));
    }

    private static bool IsValidPin(string pin)
    {
        if (pin.Length < MinPinLength) return false;
        foreach (char c in pin)
        {
            if (c < '0' || c > '9') return false;
        }
        return true;
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

    #endregion

    private void ValidateStorageMode()
    {
        if (UseDirectSeedStorage && _secureStorage is NullPlatformSecureStorage)
            throw new InvalidOperationException("DirectSecure mode requires a real IPlatformSecureStorage implementation.");
    }

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
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to deserialize storage key '{key}'.", ex);
        }
    }

    private async Task SetJsonAsync<T>(string key, T value, CancellationToken ct = default)
    {
        string json = JsonSerializer.Serialize(value);
        await _storage.SetAsync(key, json, ct);
    }

    private async Task<T?> GetSecureJsonAsync<T>(string key, CancellationToken ct = default)
    {
        string? json = await _secureStorage!.GetAsync(key, ct);
        if (string.IsNullOrEmpty(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to deserialize secure storage key '{key}'.", ex);
        }
    }

    private async Task SetSecureJsonAsync<T>(string key, T value, CancellationToken ct = default)
    {
        string json = JsonSerializer.Serialize(value);
        await _secureStorage!.SetAsync(key, json, ct);
    }

    #endregion
}
