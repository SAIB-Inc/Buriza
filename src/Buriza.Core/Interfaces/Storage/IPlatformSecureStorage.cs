namespace Buriza.Core.Interfaces.Storage;

/// <summary>
/// Platform-secure storage for sensitive data (e.g., SecureStorage/Keychain/Keystore).
/// </summary>
public interface IPlatformSecureStorage
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
