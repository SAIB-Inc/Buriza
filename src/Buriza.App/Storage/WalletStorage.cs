using System.Text.Json;
using Buriza.Core.Interfaces.Storage;
using Buriza.Data.Models.Common;

namespace Buriza.App.Storage;

public class WalletStorage(IPreferences preferences) : IWalletStorage
{
    private const string WalletsKey = "buriza_wallets";
    private const string ActiveWalletKey = "buriza_active_wallet";

    private readonly IPreferences _preferences = preferences;

    public Task SaveAsync(Wallet wallet, CancellationToken ct = default)
    {
        List<Wallet> wallets = LoadWalletsFromPreferences();

        int existingIndex = wallets.FindIndex(w => w.Id == wallet.Id);
        if (existingIndex >= 0)
        {
            wallets[existingIndex] = wallet;
        }
        else
        {
            wallets.Add(wallet);
        }

        SaveWalletsToPreferences(wallets);
        return Task.CompletedTask;
    }

    public Task<Wallet?> LoadAsync(int walletId, CancellationToken ct = default)
    {
        List<Wallet> wallets = LoadWalletsFromPreferences();
        Wallet? wallet = wallets.FirstOrDefault(w => w.Id == walletId);
        return Task.FromResult(wallet);
    }

    public Task<IReadOnlyList<Wallet>> LoadAllAsync(CancellationToken ct = default)
    {
        List<Wallet> wallets = LoadWalletsFromPreferences();
        return Task.FromResult<IReadOnlyList<Wallet>>(wallets);
    }

    public Task DeleteAsync(int walletId, CancellationToken ct = default)
    {
        List<Wallet> wallets = LoadWalletsFromPreferences();
        wallets.RemoveAll(w => w.Id == walletId);
        SaveWalletsToPreferences(wallets);
        return Task.CompletedTask;
    }

    public Task<int?> GetActiveWalletIdAsync(CancellationToken ct = default)
    {
        string? value = _preferences.Get<string?>(ActiveWalletKey, null);
        int? activeId = int.TryParse(value, out int id) ? id : null;
        return Task.FromResult(activeId);
    }

    public Task SetActiveWalletIdAsync(int walletId, CancellationToken ct = default)
    {
        _preferences.Set(ActiveWalletKey, walletId.ToString());
        return Task.CompletedTask;
    }

    private List<Wallet> LoadWalletsFromPreferences()
    {
        string? json = _preferences.Get<string?>(WalletsKey, null);
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<Wallet>>(json) ?? [];
    }

    private void SaveWalletsToPreferences(List<Wallet> wallets)
    {
        string json = JsonSerializer.Serialize(wallets);
        _preferences.Set(WalletsKey, json);
    }
}
