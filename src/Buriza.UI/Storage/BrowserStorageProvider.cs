using Buriza.Core.Interfaces.Storage;
using Microsoft.JSInterop;

namespace Buriza.UI.Storage;

/// <summary>
/// Browser storage provider for Blazor WebAssembly (Web and Extension).
/// Uses localStorage (Web) or chrome.storage.local (Extension) via JS interop.
/// Implements both IStorageProvider and ISecureStorageProvider since browser
/// storage doesn't have a separate secure storage API (data is encrypted by VaultEncryption).
/// </summary>
public class BrowserStorageProvider(IJSRuntime js) : IStorageProvider, ISecureStorageProvider
{
    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
        => await js.InvokeAsync<string?>("buriza.storage.get", ct, key);

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
        => await js.InvokeVoidAsync("buriza.storage.set", ct, key, value);

    public async Task RemoveAsync(string key, CancellationToken ct = default)
        => await js.InvokeVoidAsync("buriza.storage.remove", ct, key);

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => await js.InvokeAsync<bool>("buriza.storage.exists", ct, key);

    public async Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default)
        => await js.InvokeAsync<string[]>("buriza.storage.getKeys", ct, prefix) ?? [];

    public async Task ClearAsync(CancellationToken ct = default)
        => await js.InvokeVoidAsync("buriza.storage.clear", ct);

    // ISecureStorageProvider - Uses same storage (data is already encrypted by VaultEncryption)
    public Task<string?> GetSecureAsync(string key, CancellationToken ct = default)
        => GetAsync(key, ct);

    public Task SetSecureAsync(string key, string value, CancellationToken ct = default)
        => SetAsync(key, value, ct);

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
        => RemoveAsync(key, ct);
}
