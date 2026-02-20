using Buriza.Core.Storage;

namespace Buriza.Cli.Services;

/// <summary>
/// CLI storage service using in-memory storage.
/// All vault, auth, and config operations use the base class defaults (VaultEncryption).
/// </summary>
public sealed class BurizaCliStorageService : BurizaStorageBase
{
    private readonly InMemoryStorage _storage = new();

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
}
