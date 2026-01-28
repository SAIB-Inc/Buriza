namespace Buriza.Core.Interfaces.Storage;

public interface ISecureStorage
{
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
}

