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
    public static string Vault(int walletId) => $"buriza_vault_{walletId}";

    /// <summary>Gets the storage key for a wallet's PIN-encrypted password vault.</summary>
    public static string PinVault(int walletId) => $"buriza_pin_{walletId}";

    /// <summary>Gets the storage key for a wallet's biometric-protected password.</summary>
    public static string BiometricKey(int walletId) => $"buriza_bio_{walletId}";

    /// <summary>Gets the storage key for a chain's encrypted API key vault.</summary>
    public static string ApiKeyVault(int chain, int network) => $"buriza_apikey_{chain}_{network}";

    /// <summary>Gets the storage key for a data service's encrypted API key vault.</summary>
    public static string DataServiceApiKeyVault(int serviceType) => $"buriza_data_apikey_{serviceType}";

    #endregion

    #region Authentication State

    /// <summary>Gets the storage key for tracking failed authentication attempts.</summary>
    public static string FailedAttempts(int walletId) => $"buriza_failed_{walletId}";

    /// <summary>Gets the storage key for storing lockout end time.</summary>
    public static string Lockout(int walletId) => $"buriza_lockout_{walletId}";

    #endregion

    #region MAUI Key Tracking

    /// <summary>Key for storing the index of all tracked storage keys (MAUI only).</summary>
    public const string KeysIndex = "buriza_storage_keys_index";

    #endregion
}
