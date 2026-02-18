using Buriza.Core.Storage;
using Microsoft.JSInterop;

namespace Buriza.UI.Services;

/// <summary>
/// Web/Extension storage service using browser localStorage via JS interop.
/// All vault, auth, and config operations use the base class defaults (VaultEncryption).
/// </summary>
public sealed class BurizaWebStorageService(IJSRuntime js) : BurizaStorageBase
{
    private readonly IJSRuntime _js = js;

    public override async Task<string?> GetAsync(string key, CancellationToken ct = default)
        => await _js.InvokeAsync<string?>("buriza.storage.get", ct, key);

    public override async Task SetAsync(string key, string value, CancellationToken ct = default)
        => await _js.InvokeVoidAsync("buriza.storage.set", ct, key, value);

    public override async Task RemoveAsync(string key, CancellationToken ct = default)
        => await _js.InvokeVoidAsync("buriza.storage.remove", ct, key);

    public override async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => await _js.InvokeAsync<bool>("buriza.storage.exists", ct, key);

    public override async Task<IReadOnlyList<string>> GetKeysAsync(string prefix, CancellationToken ct = default)
        => await _js.InvokeAsync<string[]>("buriza.storage.getKeys", ct, prefix) ?? [];

    public override async Task ClearAsync(CancellationToken ct = default)
        => await _js.InvokeVoidAsync("buriza.storage.clear", ct);
}
