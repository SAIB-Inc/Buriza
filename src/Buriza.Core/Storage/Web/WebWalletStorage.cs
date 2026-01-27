using System.Text.Json;
using Buriza.Core.Interfaces.Storage;
using Buriza.Data.Models.Common;
using Microsoft.JSInterop;

namespace Buriza.Core.Storage.Web;

public class WebWalletStorage(IJSRuntime jsRuntime) : IWalletStorage
{
    private readonly IJSRuntime _js = jsRuntime;

    private const string WalletsKey = "buriza_wallets";
    private const string ActiveWalletKey = "buriza_active_wallet";

    public async Task SaveAsync(Wallet wallet, CancellationToken ct = default)
    {
        List<Wallet> wallets = [.. await LoadAllAsync(ct)];
        int existingIndex = wallets.FindIndex(w => w.Id == wallet.Id);

        if (existingIndex >= 0)
        {
            wallets[existingIndex] = wallet;
        }
        else
        {
            wallets.Add(wallet);
        }

        string json = JsonSerializer.Serialize(wallets);
        await _js.InvokeVoidAsync("localStorage.setItem", ct, WalletsKey, json);
    }

    public async Task<Wallet?> LoadAsync(int walletId, CancellationToken ct = default)
    {
        IReadOnlyList<Wallet> wallets = await LoadAllAsync(ct);
        return wallets.FirstOrDefault(w => w.Id == walletId);
    }

    public async Task<IReadOnlyList<Wallet>> LoadAllAsync(CancellationToken ct = default)
    {
        string? json = await _js.InvokeAsync<string?>("localStorage.getItem", ct, WalletsKey);
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<Wallet>>(json) ?? [];
    }

    public async Task DeleteAsync(int walletId, CancellationToken ct = default)
    {
        List<Wallet> wallets = [.. await LoadAllAsync(ct)];
        wallets.RemoveAll(w => w.Id == walletId);

        string json = JsonSerializer.Serialize(wallets);
        await _js.InvokeVoidAsync("localStorage.setItem", ct, WalletsKey, json);
    }

    public async Task<int?> GetActiveWalletIdAsync(CancellationToken ct = default)
    {
        string? value = await _js.InvokeAsync<string?>("localStorage.getItem", ct, ActiveWalletKey);
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return int.TryParse(value, out int id) ? id : null;
    }

    public async Task SetActiveWalletIdAsync(int walletId, CancellationToken ct = default)
    {
        await _js.InvokeVoidAsync("localStorage.setItem", ct, ActiveWalletKey, walletId.ToString());
    }
}
