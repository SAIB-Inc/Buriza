using Buriza.Core.Interfaces.Storage;
using Microsoft.JSInterop;

namespace Buriza.UI.Storage;

/// <summary>
/// Browser storage for Blazor WebAssembly (Web and Extension).
/// Uses localStorage (Web) or chrome.storage.local (Extension) via JS interop.
/// </summary>
public class BrowserPlatformStorage(IJSRuntime js) : IPlatformStorage, IPlatformSecureStorage
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
}
