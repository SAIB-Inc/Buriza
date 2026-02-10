namespace Buriza.Core.Interfaces.Storage;

/// <summary>
/// Secure storage provider for sensitive data like encrypted vaults.
/// All implementations route to IPlatformStorage (Preferences on MAUI, localStorage on Web).
/// Data confidentiality relies on VaultEncryption (Argon2id + AES-256-GCM), not the storage backend.
/// </summary>
public interface ISecureStorageProvider
{
    /// <summary>Gets a secure string value by key. Returns null if not found.</summary>
    Task<string?> GetSecureAsync(string key, CancellationToken ct = default);

    /// <summary>Sets a secure string value by key.</summary>
    Task SetSecureAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Removes a secure value by key.</summary>
    Task RemoveSecureAsync(string key, CancellationToken ct = default);
}
