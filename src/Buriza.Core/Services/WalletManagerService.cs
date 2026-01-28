using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Crypto;
using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Models;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Services;

public class WalletManagerService(
    IWalletStorage walletStorage,
    ISecureStorage secureStorage,
    ChainProviderRegistry providerRegistry,
    ISessionService sessionService) : IWalletManager
{
    private readonly IWalletStorage _walletStorage = walletStorage;
    private readonly ISecureStorage _secureStorage = secureStorage;
    private readonly ChainProviderRegistry _providerRegistry = providerRegistry;
    private readonly ISessionService _sessionService = sessionService;

    private const int GapLimit = 20;

    #region Wallet Operations

    public async Task<Wallet> CreateAsync(string name, string mnemonic, string password, ChainType initialChain = ChainType.Cardano, CancellationToken ct = default)
    {
        IChainProvider provider = _providerRegistry.GetProvider(initialChain);

        bool isValid = await provider.KeyService.ValidateMnemonicAsync(mnemonic);
        if (!isValid)
        {
            throw new ArgumentException("Invalid mnemonic", nameof(mnemonic));
        }

        IReadOnlyList<Wallet> existingWallets = await _walletStorage.LoadAllAsync(ct);
        int newId = existingWallets.Count > 0 ? existingWallets.Max(w => w.Id) + 1 : 1;

        string derivationPath = GetDerivationPath(initialChain, 0, 0, 0);

        Wallet wallet = new()
        {
            Id = newId,
            Name = name,
            ActiveChain = initialChain,
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

        // Derive and cache addresses for account 0 on the initial chain
        await DeriveAndCacheAddressesAsync(wallet.Id, initialChain, provider, 0, mnemonic, ct);

        return wallet;
    }

    public async Task<Wallet> ImportAsync(string name, string mnemonic, string password, ChainType initialChain = ChainType.Cardano, CancellationToken ct = default)
    {
        return await CreateAsync(name, mnemonic, password, initialChain, ct);
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

    #endregion

    #region Chain Management

    public async Task SetActiveChainAsync(int walletId, ChainType chain, string password, CancellationToken ct = default)
    {
        Wallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        IChainProvider provider = _providerRegistry.GetProvider(chain);

        // Update active chain
        wallet.ActiveChain = chain;
        await _walletStorage.SaveAsync(wallet, ct);

        // Check if account 0 needs to be unlocked for this chain
        WalletAccount? activeAccount = wallet.Accounts.FirstOrDefault(a => a.IsActive);
        int accountIndex = activeAccount?.Index ?? 0;

        if (!_sessionService.HasCachedAddresses(walletId, chain, accountIndex))
        {
            // Derive and cache addresses for the new chain
            byte[] mnemonicBytes = await _secureStorage.UnlockVaultAsync(walletId, password, ct);
            try
            {
                string mnemonic = Encoding.UTF8.GetString(mnemonicBytes);
                await DeriveAndCacheAddressesAsync(walletId, chain, provider, accountIndex, mnemonic, ct);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(mnemonicBytes);
            }
        }
    }

    public IReadOnlyList<ChainType> GetAvailableChains()
    {
        return _providerRegistry.GetAllProviders()
            .Select(p => p.ChainInfo.Chain)
            .ToList();
    }

    #endregion

    #region Account Operations

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
            DerivationPath = GetDerivationPath(wallet.ActiveChain, newIndex, 0, 0),
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

    #endregion

    #region Unlock Operations

    public async Task UnlockAccountAsync(int walletId, int accountIndex, string password, CancellationToken ct = default)
    {
        Wallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        IChainProvider provider = _providerRegistry.GetProvider(wallet.ActiveChain);

        // Verify account exists
        if (!wallet.Accounts.Any(a => a.Index == accountIndex))
        {
            throw new ArgumentException($"Account {accountIndex} not found in wallet {walletId}", nameof(accountIndex));
        }

        // Derive and cache addresses for the active chain
        byte[] mnemonicBytes = await _secureStorage.UnlockVaultAsync(walletId, password, ct);
        try
        {
            string mnemonic = Encoding.UTF8.GetString(mnemonicBytes);
            await DeriveAndCacheAddressesAsync(walletId, wallet.ActiveChain, provider, accountIndex, mnemonic, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    #endregion

    #region Address Operations

    public async Task<IReadOnlyList<DerivedAddress>> GetAddressesAsync(int walletId, int accountIndex, int count = 20, bool includeChange = false, CancellationToken ct = default)
    {
        Wallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        ChainType chain = wallet.ActiveChain;
        EnsureAccountUnlocked(walletId, chain, accountIndex);

        List<DerivedAddress> addresses = [];

        // External addresses (receive)
        for (int i = 0; i < count; i++)
        {
            string? address = _sessionService.GetCachedAddress(walletId, chain, accountIndex, i, false);
            if (address != null)
            {
                addresses.Add(new DerivedAddress
                {
                    Address = address,
                    AccountIndex = accountIndex,
                    Role = AddressRole.External,
                    AddressIndex = i,
                    DerivationPath = GetDerivationPath(chain, accountIndex, 0, i)
                });
            }
        }

        // Internal addresses (change)
        if (includeChange)
        {
            for (int i = 0; i < count; i++)
            {
                string? address = _sessionService.GetCachedAddress(walletId, chain, accountIndex, i, true);
                if (address != null)
                {
                    addresses.Add(new DerivedAddress
                    {
                        Address = address,
                        AccountIndex = accountIndex,
                        Role = AddressRole.Internal,
                        AddressIndex = i,
                        DerivationPath = GetDerivationPath(chain, accountIndex, 1, i)
                    });
                }
            }
        }

        return addresses;
    }

    public async Task<DerivedAddress> GetNextReceiveAddressAsync(int walletId, int accountIndex, CancellationToken ct = default)
    {
        Wallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        ChainType chain = wallet.ActiveChain;
        IChainProvider provider = _providerRegistry.GetProvider(chain);
        EnsureAccountUnlocked(walletId, chain, accountIndex);

        int? firstUnusedIndex = null;
        int consecutiveUnused = 0;

        for (int index = 0; consecutiveUnused < GapLimit; index++)
        {
            string? address = _sessionService.GetCachedAddress(walletId, chain, accountIndex, index, false);
            if (address == null) break;

            bool isUsed = await provider.QueryService.IsAddressUsedAsync(address, ct);

            if (isUsed)
            {
                consecutiveUnused = 0;
            }
            else
            {
                firstUnusedIndex ??= index;
                consecutiveUnused++;
            }
        }

        int resultIndex = firstUnusedIndex ?? 0;
        string? resultAddress = _sessionService.GetCachedAddress(walletId, chain, accountIndex, resultIndex, false);

        return new DerivedAddress
        {
            Address = resultAddress!,
            AccountIndex = accountIndex,
            Role = AddressRole.External,
            AddressIndex = resultIndex,
            DerivationPath = GetDerivationPath(chain, accountIndex, 0, resultIndex),
            IsUsed = false
        };
    }

    public async Task<DerivedAddress> GetChangeAddressAsync(int walletId, int accountIndex, CancellationToken ct = default)
    {
        Wallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        ChainType chain = wallet.ActiveChain;
        IChainProvider provider = _providerRegistry.GetProvider(chain);
        EnsureAccountUnlocked(walletId, chain, accountIndex);

        // Find first unused change address
        for (int index = 0; index < GapLimit; index++)
        {
            string? address = _sessionService.GetCachedAddress(walletId, chain, accountIndex, index, true);
            if (address == null) break;

            bool isUsed = await provider.QueryService.IsAddressUsedAsync(address, ct);

            if (!isUsed)
            {
                return new DerivedAddress
                {
                    Address = address,
                    AccountIndex = accountIndex,
                    Role = AddressRole.Internal,
                    AddressIndex = index,
                    DerivationPath = GetDerivationPath(chain, accountIndex, 1, index),
                    IsUsed = false
                };
            }
        }

        // Fallback to index 0 if all within gap limit are used
        string? fallbackAddress = _sessionService.GetCachedAddress(walletId, chain, accountIndex, 0, true);
        return new DerivedAddress
        {
            Address = fallbackAddress!,
            AccountIndex = accountIndex,
            Role = AddressRole.Internal,
            AddressIndex = 0,
            DerivationPath = GetDerivationPath(chain, accountIndex, 1, 0),
            IsUsed = true
        };
    }

    public async Task<bool> IsOwnAddressAsync(int walletId, string address, CancellationToken ct = default)
    {
        Wallet wallet = await GetWalletOrThrowAsync(walletId, ct);

        // Check across all chains and accounts that are cached
        foreach (ChainType chain in GetAvailableChains())
        {
            foreach (WalletAccount account in wallet.Accounts)
            {
                // Skip accounts that aren't unlocked for this chain
                if (!_sessionService.HasCachedAddresses(walletId, chain, account.Index))
                    continue;

                for (int i = 0; i < GapLimit; i++)
                {
                    string? derivedExternal = _sessionService.GetCachedAddress(walletId, chain, account.Index, i, false);
                    if (derivedExternal == address) return true;

                    string? derivedInternal = _sessionService.GetCachedAddress(walletId, chain, account.Index, i, true);
                    if (derivedInternal == address) return true;
                }
            }
        }

        return false;
    }

    #endregion

    #region Balance & Assets

    public async Task<ulong> GetBalanceAsync(int walletId, int accountIndex, bool allAddresses = false, CancellationToken ct = default)
    {
        Wallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        ChainType chain = wallet.ActiveChain;
        IChainProvider provider = _providerRegistry.GetProvider(chain);
        EnsureAccountUnlocked(walletId, chain, accountIndex);

        if (!allAddresses)
        {
            // Fast path: query only the first receive address
            string? address = _sessionService.GetCachedAddress(walletId, chain, accountIndex, 0, false);
            return address != null ? await provider.QueryService.GetBalanceAsync(address, ct) : 0;
        }

        // Full scan: query all addresses in parallel
        IReadOnlyList<DerivedAddress> addresses = await GetAddressesAsync(walletId, accountIndex, GapLimit, true, ct);
        IEnumerable<Task<ulong>> tasks = addresses.Select(a => provider.QueryService.GetBalanceAsync(a.Address, ct));
        ulong[] balances = await Task.WhenAll(tasks);

        return balances.Aggregate(0UL, (total, b) => total + b);
    }

    public async Task<IReadOnlyList<Asset>> GetAssetsAsync(int walletId, int accountIndex, bool allAddresses = false, CancellationToken ct = default)
    {
        Wallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        ChainType chain = wallet.ActiveChain;
        IChainProvider provider = _providerRegistry.GetProvider(chain);
        EnsureAccountUnlocked(walletId, chain, accountIndex);

        if (!allAddresses)
        {
            // Fast path: query only the first receive address
            string? address = _sessionService.GetCachedAddress(walletId, chain, accountIndex, 0, false);
            return address != null ? await provider.QueryService.GetAssetsAsync(address, ct) : [];
        }

        // Full scan: query all addresses in parallel
        IReadOnlyList<DerivedAddress> addresses = await GetAddressesAsync(walletId, accountIndex, GapLimit, true, ct);
        IEnumerable<Task<IReadOnlyList<Asset>>> tasks = addresses.Select(a => provider.QueryService.GetAssetsAsync(a.Address, ct));
        IReadOnlyList<Asset>[] results = await Task.WhenAll(tasks);

        // Aggregate assets from all addresses
        return [.. results
            .SelectMany(assets => assets)
            .GroupBy(a => a.Subject)
            .Select(g => g.First() with { Quantity = g.Aggregate(0UL, (sum, a) => sum + a.Quantity) })];
    }

    #endregion

    #region Sensitive Operations (Require Password)

    public async Task<byte[]> SignTransactionAsync(int walletId, int accountIndex, int addressIndex, UnsignedTransaction unsignedTx, string password, CancellationToken ct = default)
    {
        Wallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        IChainProvider provider = _providerRegistry.GetProvider(wallet.ActiveChain);

        byte[] mnemonicBytes = await _secureStorage.UnlockVaultAsync(walletId, password, ct);
        try
        {
            string mnemonic = Encoding.UTF8.GetString(mnemonicBytes);
            var privateKey = await provider.KeyService.DerivePrivateKeyAsync(mnemonic, accountIndex, addressIndex);
            return await provider.TransactionService.SignAsync(unsignedTx, privateKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    public async Task<string> ExportMnemonicAsync(int walletId, string password, CancellationToken ct = default)
    {
        await GetWalletOrThrowAsync(walletId, ct);

        byte[] mnemonicBytes = await _secureStorage.UnlockVaultAsync(walletId, password, ct);
        try
        {
            return Encoding.UTF8.GetString(mnemonicBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    #endregion

    #region Private Helpers

    private async Task<Wallet> GetWalletOrThrowAsync(int walletId, CancellationToken ct)
    {
        return await _walletStorage.LoadAsync(walletId, ct)
            ?? throw new ArgumentException($"Wallet {walletId} not found", nameof(walletId));
    }

    private void EnsureAccountUnlocked(int walletId, ChainType chain, int accountIndex)
    {
        if (!_sessionService.HasCachedAddresses(walletId, chain, accountIndex))
        {
            throw new InvalidOperationException($"Account {accountIndex} is not unlocked for {chain}. Call UnlockAccountAsync or SetActiveChainAsync first.");
        }
    }

    private async Task DeriveAndCacheAddressesAsync(int walletId, ChainType chain, IChainProvider provider, int accountIndex, string mnemonic, CancellationToken ct)
    {
        // Derive and cache external addresses (receive)
        for (int i = 0; i < GapLimit; i++)
        {
            string address = await provider.KeyService.DeriveAddressAsync(mnemonic, accountIndex, i, false);
            _sessionService.CacheAddress(walletId, chain, accountIndex, i, false, address);
        }

        // Derive and cache internal addresses (change)
        for (int i = 0; i < GapLimit; i++)
        {
            string address = await provider.KeyService.DeriveAddressAsync(mnemonic, accountIndex, i, true);
            _sessionService.CacheAddress(walletId, chain, accountIndex, i, true, address);
        }
    }

    private static string GetDerivationPath(ChainType chain, int account, int role, int index)
    {
        return chain switch
        {
            ChainType.Cardano => CardanoDerivation.GetPath(account, role, index),
            _ => throw new NotSupportedException($"Chain {chain} is not supported")
        };
    }

    #endregion
}
