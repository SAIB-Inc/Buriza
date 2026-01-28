using Buriza.Core.Models;

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
}
