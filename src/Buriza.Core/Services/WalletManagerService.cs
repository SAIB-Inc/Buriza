using Buriza.Core.Crypto;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Services;

public class WalletManagerService(
    IWalletStorage walletStorage,
    ISecureStorage secureStorage,
    IKeyManager keyManager,
    ChainProviderRegistry providerRegistry,
    SessionService sessionService) : IWalletManager
{
    private readonly IWalletStorage _walletStorage = walletStorage;
    private readonly ISecureStorage _secureStorage = secureStorage;
    private readonly IKeyManager _keyManager = keyManager;
    private readonly ChainProviderRegistry _providerRegistry = providerRegistry;
    private readonly SessionService _sessionService = sessionService;

    public async Task<Wallet> CreateAsync(string name, string mnemonic, string password, ChainType chain, CancellationToken ct = default)
    {
        bool isValid = await _keyManager.ValidateMnemonicAsync(mnemonic);
        if (!isValid)
        {
            throw new ArgumentException("Invalid mnemonic", nameof(mnemonic));
        }

        IReadOnlyList<Wallet> existingWallets = await _walletStorage.LoadAllAsync(ct);
        int newId = existingWallets.Count > 0 ? existingWallets.Max(w => w.Id) + 1 : 1;

        string derivationPath = GetDerivationPath(chain, 0, 0, 0);

        Wallet wallet = new()
        {
            Id = newId,
            Name = name,
            ChainType = chain,
            CreatedAt = DateTime.UtcNow,
            Accounts =
            [
                new WalletAccount
                {
                    Index = 0,
                    Name = "Account 1",
                    DerivationPath = derivationPath,
                    IsActive = true
                }
            ]
        };

        await _secureStorage.CreateVaultAsync(wallet.Id, mnemonic, password, ct);
        await _walletStorage.SaveAsync(wallet, ct);
        await _walletStorage.SetActiveWalletIdAsync(wallet.Id, ct);

        _sessionService.Unlock(password);

        return wallet;
    }

    public async Task<Wallet> ImportAsync(string name, string mnemonic, string password, ChainType chain, CancellationToken ct = default)
    {
        return await CreateAsync(name, mnemonic, password, chain, ct);
    }

    public async Task<IReadOnlyList<Wallet>> GetAllAsync(CancellationToken ct = default)
    {
        return await _walletStorage.LoadAllAsync(ct);
    }

    public async Task<Wallet?> GetActiveAsync(CancellationToken ct = default)
    {
        int? activeId = await _walletStorage.GetActiveWalletIdAsync(ct);
        if (activeId == null)
        {
            return null;
        }

        return await _walletStorage.LoadAsync(activeId.Value, ct);
    }

    public async Task SetActiveAsync(int walletId, CancellationToken ct = default)
    {
        Wallet? wallet = await _walletStorage.LoadAsync(walletId, ct);
        if (wallet == null)
        {
            throw new ArgumentException($"Wallet {walletId} not found", nameof(walletId));
        }

        await _walletStorage.SetActiveWalletIdAsync(walletId, ct);
    }

    public async Task DeleteAsync(int walletId, CancellationToken ct = default)
    {
        await _secureStorage.DeleteVaultAsync(walletId, ct);
        await _walletStorage.DeleteAsync(walletId, ct);
    }

    public async Task<WalletAccount> CreateAccountAsync(int walletId, string name, CancellationToken ct = default)
    {
        Wallet? wallet = await _walletStorage.LoadAsync(walletId, ct);
        if (wallet == null)
        {
            throw new ArgumentException($"Wallet {walletId} not found", nameof(walletId));
        }

        int newIndex = wallet.Accounts.Count > 0 ? wallet.Accounts.Max(a => a.Index) + 1 : 0;

        WalletAccount account = new()
        {
            Index = newIndex,
            Name = name,
            DerivationPath = GetDerivationPath(wallet.ChainType, newIndex, 0, 0),
            IsActive = false
        };

        wallet.Accounts.Add(account);
        await _walletStorage.SaveAsync(wallet, ct);

        return account;
    }

    public async Task<WalletAccount?> GetActiveAccountAsync(CancellationToken ct = default)
    {
        Wallet? wallet = await GetActiveAsync(ct);
        return wallet?.Accounts.FirstOrDefault(a => a.IsActive);
    }

    public async Task SetActiveAccountAsync(int walletId, int accountIndex, CancellationToken ct = default)
    {
        Wallet? wallet = await _walletStorage.LoadAsync(walletId, ct);
        if (wallet == null)
        {
            throw new ArgumentException($"Wallet {walletId} not found", nameof(walletId));
        }

        foreach (WalletAccount account in wallet.Accounts)
        {
            account.IsActive = account.Index == accountIndex;
        }

        await _walletStorage.SaveAsync(wallet, ct);
    }

    private static string GetDerivationPath(ChainType chain, int account, int role, int index)
    {
        return chain switch
        {
            ChainType.Cardano => CardanoDerivation.GetPath(account, role, index),
            _ => throw new NotSupportedException($"Chain {chain} is not supported")
        };
    }
}
