using Buriza.Core.Models;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Interfaces.Storage;

public interface IWalletStorage
{
    #region Wallet Metadata

    Task SaveAsync(BurizaWallet wallet, CancellationToken ct = default);
    Task<BurizaWallet?> LoadAsync(int walletId, CancellationToken ct = default);
    Task<IReadOnlyList<BurizaWallet>> LoadAllAsync(CancellationToken ct = default);
    Task DeleteAsync(int walletId, CancellationToken ct = default);
    Task<int?> GetActiveWalletIdAsync(CancellationToken ct = default);
    Task SetActiveWalletIdAsync(int walletId, CancellationToken ct = default);
    Task ClearActiveWalletIdAsync(CancellationToken ct = default);

    /// <summary>
    /// Generates the next available wallet ID atomically.
    /// </summary>
    Task<int> GenerateNextIdAsync(CancellationToken ct = default);

    #endregion

    #region Secure Vault

    Task<bool> HasVaultAsync(int walletId, CancellationToken ct = default);
    Task CreateVaultAsync(int walletId, string mnemonic, string password, CancellationToken ct = default);

    /// <summary>
    /// Unlocks the vault and returns the mnemonic as a byte array.
    /// IMPORTANT: Caller MUST zero the returned bytes after use with CryptographicOperations.ZeroMemory()
    /// </summary>
    Task<byte[]> UnlockVaultAsync(int walletId, string password, CancellationToken ct = default);

    Task DeleteVaultAsync(int walletId, CancellationToken ct = default);
    Task<bool> VerifyPasswordAsync(int walletId, string password, CancellationToken ct = default);
    Task ChangePasswordAsync(int walletId, string oldPassword, string newPassword, CancellationToken ct = default);

    #endregion

    #region Custom Provider Config

    /// <summary>Gets custom provider config (endpoint, name). API key stored separately encrypted.</summary>
    Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainType chain, NetworkType network, CancellationToken ct = default);

    /// <summary>Saves custom provider config (endpoint, name). Use SaveCustomApiKeyAsync for API key.</summary>
    Task SaveCustomProviderConfigAsync(CustomProviderConfig config, CancellationToken ct = default);

    /// <summary>Deletes custom provider config for a chain/network.</summary>
    Task DeleteCustomProviderConfigAsync(ChainType chain, NetworkType network, CancellationToken ct = default);

    /// <summary>Saves encrypted custom API key. Requires password for encryption.</summary>
    Task SaveCustomApiKeyAsync(ChainType chain, NetworkType network, string apiKey, string password, CancellationToken ct = default);

    /// <summary>
    /// Decrypts and returns custom API key as bytes.
    /// IMPORTANT: Caller MUST zero the returned bytes after use.
    /// Returns null if no custom API key is set.
    /// </summary>
    Task<byte[]?> UnlockCustomApiKeyAsync(ChainType chain, NetworkType network, string password, CancellationToken ct = default);

    /// <summary>Deletes encrypted custom API key for a chain/network.</summary>
    Task DeleteCustomApiKeyAsync(ChainType chain, NetworkType network, CancellationToken ct = default);

    #endregion

    #region Custom Data Service Config

    /// <summary>Gets custom data service config (endpoint, name). API key stored separately encrypted.</summary>
    Task<DataServiceConfig?> GetDataServiceConfigAsync(DataServiceType serviceType, CancellationToken ct = default);

    /// <summary>Gets all custom data service configs.</summary>
    Task<IReadOnlyList<DataServiceConfig>> GetAllDataServiceConfigsAsync(CancellationToken ct = default);

    /// <summary>Saves custom data service config (endpoint, name). Use SaveDataServiceApiKeyAsync for API key.</summary>
    Task SaveDataServiceConfigAsync(DataServiceConfig config, CancellationToken ct = default);

    /// <summary>Deletes custom data service config.</summary>
    Task DeleteDataServiceConfigAsync(DataServiceType serviceType, CancellationToken ct = default);

    /// <summary>Saves encrypted custom API key for a data service. Requires password for encryption.</summary>
    Task SaveDataServiceApiKeyAsync(DataServiceType serviceType, string apiKey, string password, CancellationToken ct = default);

    /// <summary>
    /// Decrypts and returns custom API key as bytes.
    /// IMPORTANT: Caller MUST zero the returned bytes after use.
    /// Returns null if no custom API key is set.
    /// </summary>
    Task<byte[]?> UnlockDataServiceApiKeyAsync(DataServiceType serviceType, string password, CancellationToken ct = default);

    /// <summary>Deletes encrypted custom API key for a data service.</summary>
    Task DeleteDataServiceApiKeyAsync(DataServiceType serviceType, CancellationToken ct = default);

    #endregion
}
