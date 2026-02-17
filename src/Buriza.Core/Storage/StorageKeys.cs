namespace Buriza.Core.Storage;

/// <summary>
/// Centralized storage key definitions for all Buriza storage operations.
/// Prevents typos and provides single source of truth for key naming.
/// </summary>
public static class StorageKeys
{
    #region Wallet Metadata

    /// <summary>Key for storing all wallet metadata as JSON array.</summary>
    public const string Wallets = "buriza_wallets";

    /// <summary>Key for storing the active wallet ID.</summary>
    public const string ActiveWallet = "buriza_active_wallet";

    /// <summary>Key for storing custom provider configurations.</summary>
    public const string CustomConfigs = "buriza_custom_configs";

    /// <summary>Key for storing data service configurations.</summary>
    public const string DataServiceConfigs = "buriza_data_service_configs";

    #endregion

    #region Secure Vaults

    /// <summary>Gets the storage key for a wallet's encrypted mnemonic vault.</summary>
    public static string Vault(Guid walletId) => $"buriza_vault_{walletId:N}";

    /// <summary>Gets the storage key for a wallet's PIN-encrypted password vault.</summary>
    public static string PinVault(Guid walletId) => $"buriza_pin_{walletId:N}";

    /// <summary>Gets the storage key for a wallet's biometric-protected password.</summary>
    public static string BiometricKey(Guid walletId) => $"buriza_bio_{walletId:N}";

    /// <summary>Gets the storage key for a wallet's biometric-encrypted seed vault.</summary>
    public static string BiometricSeedVault(Guid walletId) => $"buriza_bio_seed_{walletId:N}";

    /// <summary>Gets the storage key for a wallet's biometric-protected seed (direct storage).</summary>
    public static string BiometricSeed(Guid walletId) => $"buriza_bio_seed_raw_{walletId:N}";

    /// <summary>Gets the secure storage key for a wallet's seed (direct storage).</summary>
    public static string SecureSeed(Guid walletId) => $"buriza_secure_seed_{walletId:N}";

    /// <summary>Gets the storage key for a wallet's Argon2id password verifier (MAUI only).</summary>
    public static string PasswordVerifier(Guid walletId) => $"buriza_pw_verifier_{walletId:N}";

    /// <summary>Gets the storage key for a chain's encrypted API key vault.</summary>
    public static string ApiKeyVault(int chain, int network) => $"buriza_apikey_{chain}_{network}";

    /// <summary>Gets the storage key for a data service's encrypted API key vault.</summary>
    public static string DataServiceApiKeyVault(int serviceType) => $"buriza_data_apikey_{serviceType}";

    #endregion

    #region Authentication State

    /// <summary>Gets the storage key for a wallet's authentication type (password/pin/biometric).</summary>
    public static string AuthType(Guid walletId) => $"buriza_auth_type_{walletId:N}";

    /// <summary>Gets the storage key for a wallet's authentication type HMAC.</summary>
    public static string AuthTypeHmac(Guid walletId) => $"buriza_auth_type_hmac_{walletId:N}";

    /// <summary>Gets the storage key for HMAC-protected lockout state (Preferences).</summary>
    public static string LockoutState(Guid walletId) => $"buriza_lockout_state_{walletId:N}";

    /// <summary>Gets the secure storage key for lockout-active flag (SecureStorage). Detects Preferences deletion.</summary>
    public static string LockoutActive(Guid walletId) => $"buriza_lockout_active_{walletId:N}";

    /// <summary>Key for storing the lockout HMAC key (installation scoped).</summary>
    public const string LockoutKey = "buriza_lockout_hmac_key";

    /// <summary>Key for storing the auth-type HMAC key (installation scoped).</summary>
    public const string AuthTypeKey = "buriza_auth_type_hmac_key";

    #endregion

    #region MAUI Key Tracking

    /// <summary>Key for storing the index of all tracked storage keys (MAUI only).</summary>
    public const string KeysIndex = "buriza_storage_keys_index";

    /// <summary>Key for storing the index of all tracked secure storage keys (MAUI only).</summary>
    public const string SecureKeysIndex = "buriza_secure_storage_keys_index";

    #endregion
}
