using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Crypto;
using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Models;
using Buriza.Data.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Transaction = Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction;

namespace Buriza.Core.Services;

public class WalletManagerService(
    IWalletStorage storage,
    ChainProviderRegistry providerRegistry,
    IKeyService keyService,
    ISessionService sessionService) : IWalletManager
{
    private readonly IWalletStorage _storage = storage;
    private readonly ChainProviderRegistry _providerRegistry = providerRegistry;
    private readonly IKeyService _keyService = keyService;
    private readonly ISessionService _sessionService = sessionService;

    private const int GapLimit = 20;

    #region Wallet Lifecycle

    public async Task<BurizaWallet> CreateAsync(string name, string password, ChainType initialChain = ChainType.Cardano, int mnemonicWordCount = 24, CancellationToken ct = default)
    {
        string mnemonic = _keyService.GenerateMnemonic(mnemonicWordCount);
        return await ImportAsync(name, mnemonic, password, initialChain, ct);
    }

    public async Task<BurizaWallet> ImportAsync(string name, string mnemonic, string password, ChainType initialChain = ChainType.Cardano, CancellationToken ct = default)
    {
        if (!_keyService.ValidateMnemonic(mnemonic))
            throw new ArgumentException("Invalid mnemonic", nameof(mnemonic));

        IReadOnlyList<BurizaWallet> existingWallets = await _storage.LoadAllAsync(ct);
        int newId = existingWallets.Count > 0 ? existingWallets.Max(w => w.Id) + 1 : 1;

        Task<string>[] externalTasks = [.. Enumerable.Range(0, GapLimit).Select(i => _keyService.DeriveAddressAsync(mnemonic, initialChain, 0, i, false, ct))];
        Task<string>[] internalTasks = [.. Enumerable.Range(0, GapLimit).Select(i => _keyService.DeriveAddressAsync(mnemonic, initialChain, 0, i, true, ct))];

        await Task.WhenAll(externalTasks.Concat(internalTasks));

        List<AddressInfo> externalAddresses = [.. externalTasks.Select((t, i) => new AddressInfo { Index = i, Address = t.Result })];
        List<AddressInfo> internalAddresses = [.. internalTasks.Select((t, i) => new AddressInfo { Index = i, Address = t.Result })];

        // Cache addresses
        for (int i = 0; i < GapLimit; i++)
        {
            _sessionService.CacheAddress(newId, initialChain, 0, i, false, externalAddresses[i].Address);
            _sessionService.CacheAddress(newId, initialChain, 0, i, true, internalAddresses[i].Address);
        }

        ChainAddressData chainData = new()
        {
            Chain = initialChain,
            ExternalAddresses = externalAddresses,
            InternalAddresses = internalAddresses,
            LastSyncedAt = DateTime.UtcNow
        };

        BurizaWallet wallet = new()
        {
            Id = newId,
            Name = name,
            ActiveChain = initialChain,
            ActiveAccountIndex = 0,
            Accounts =
            [
                new WalletAccount
                {
                    Index = 0,
                    Name = "Account 1",
                    ChainData = new Dictionary<ChainType, ChainAddressData> { [initialChain] = chainData }
                }
            ]
        };

        await _storage.CreateVaultAsync(wallet.Id, mnemonic, password, ct);
        await _storage.SaveAsync(wallet, ct);
        await _storage.SetActiveWalletIdAsync(wallet.Id, ct);

        AttachProvider(wallet);
        return wallet;
    }

    public async Task<IReadOnlyList<BurizaWallet>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<BurizaWallet> wallets = await _storage.LoadAllAsync(ct);
        foreach (BurizaWallet wallet in wallets)
            AttachProvider(wallet);
        return wallets;
    }

    public async Task<BurizaWallet?> GetActiveAsync(CancellationToken ct = default)
    {
        int? activeId = await _storage.GetActiveWalletIdAsync(ct);
        if (!activeId.HasValue) return null;

        BurizaWallet? wallet = await _storage.LoadAsync(activeId.Value, ct);
        if (wallet != null)
            AttachProvider(wallet);
        return wallet;
    }

    public async Task SetActiveAsync(int walletId, CancellationToken ct = default)
    {
        _ = await GetWalletOrThrowAsync(walletId, ct);
        await _storage.SetActiveWalletIdAsync(walletId, ct);
    }

    public async Task DeleteAsync(int walletId, CancellationToken ct = default)
    {
        await _storage.DeleteVaultAsync(walletId, ct);
        await _storage.DeleteAsync(walletId, ct);
    }

    #endregion

    #region Chain Management

    public async Task SetActiveChainAsync(int walletId, ChainType chain, string password, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        IChainProvider provider = _providerRegistry.GetProvider(chain);

        WalletAccount? account = wallet.GetActiveAccount()
            ?? throw new InvalidOperationException("No active account");

        // Derive addresses for this chain if needed
        if (account.GetChainData(chain) == null)
        {
            byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, ct);
            try
            {
                string mnemonic = Encoding.UTF8.GetString(mnemonicBytes);
                await DeriveAndSaveChainDataAsync(wallet, account.Index, chain, mnemonic, ct);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(mnemonicBytes);
            }
        }

        wallet.ActiveChain = chain;
        wallet.LastAccessedAt = DateTime.UtcNow;
        await _storage.SaveAsync(wallet, ct);

        // Update provider reference
        AttachProvider(wallet);
    }

    public IReadOnlyList<ChainType> GetAvailableChains()
        => _providerRegistry.GetAllProviders().Select(p => p.ChainInfo.Chain).ToList();

    #endregion

    #region Account Management

    public async Task<WalletAccount> CreateAccountAsync(int walletId, string name, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);

        int newIndex = wallet.Accounts.Count > 0 ? wallet.Accounts.Max(a => a.Index) + 1 : 0;

        WalletAccount account = new()
        {
            Index = newIndex,
            Name = name
        };

        wallet.Accounts.Add(account);
        await _storage.SaveAsync(wallet, ct);

        return account;
    }

    public async Task<WalletAccount?> GetActiveAccountAsync(CancellationToken ct = default)
    {
        BurizaWallet? wallet = await GetActiveAsync(ct);
        return wallet?.GetActiveAccount();
    }

    public async Task SetActiveAccountAsync(int walletId, int accountIndex, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);

        if (!wallet.Accounts.Any(a => a.Index == accountIndex))
            throw new ArgumentException($"Account {accountIndex} not found", nameof(accountIndex));

        wallet.ActiveAccountIndex = accountIndex;
        wallet.LastAccessedAt = DateTime.UtcNow;
        await _storage.SaveAsync(wallet, ct);
    }

    public async Task UnlockAccountAsync(int walletId, int accountIndex, string password, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        WalletAccount? account = wallet.Accounts.FirstOrDefault(a => a.Index == accountIndex)
            ?? throw new ArgumentException($"Account {accountIndex} not found", nameof(accountIndex));

        ChainType chain = wallet.ActiveChain;
        IChainProvider provider = _providerRegistry.GetProvider(chain);

        // Skip if already unlocked
        if (account.GetChainData(chain) != null && _sessionService.HasCachedAddresses(walletId, chain, accountIndex))
            return;

        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, ct);
        try
        {
            string mnemonic = Encoding.UTF8.GetString(mnemonicBytes);
            await DeriveAndSaveChainDataAsync(wallet, accountIndex, chain, mnemonic, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    #endregion

    #region Sensitive Operations (Requires Password)

    public async Task<Transaction> SignTransactionAsync(int walletId, int accountIndex, int addressIndex, UnsignedTransaction unsignedTx, string password, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        IChainProvider provider = _providerRegistry.GetProvider(wallet.ActiveChain);

        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, ct);
        try
        {
            string mnemonic = Encoding.UTF8.GetString(mnemonicBytes);
            PrivateKey privateKey = await _keyService.DerivePrivateKeyAsync(mnemonic, wallet.ActiveChain, accountIndex, addressIndex, ct: ct);
            return await provider.TransactionService.SignAsync(unsignedTx, privateKey, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    public async Task<string> ExportMnemonicAsync(int walletId, string password, CancellationToken ct = default)
    {
        await GetWalletOrThrowAsync(walletId, ct);

        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, ct);
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

    private async Task<BurizaWallet> GetWalletOrThrowAsync(int walletId, CancellationToken ct)
    {
        BurizaWallet wallet = await _storage.LoadAsync(walletId, ct)
            ?? throw new ArgumentException($"Wallet {walletId} not found", nameof(walletId));
        AttachProvider(wallet);
        return wallet;
    }

    private void AttachProvider(BurizaWallet wallet)
        => wallet.Provider = _providerRegistry.GetProvider(wallet.ActiveChain);

    private async Task DeriveAndSaveChainDataAsync(BurizaWallet wallet, int accountIndex, ChainType chain, string mnemonic, CancellationToken ct)
    {
        // Derive all addresses in parallel
        Task<string>[] externalTasks = [.. Enumerable.Range(0, GapLimit).Select(i => _keyService.DeriveAddressAsync(mnemonic, chain, accountIndex, i, false, ct))];
        Task<string>[] internalTasks = [.. Enumerable.Range(0, GapLimit).Select(i => _keyService.DeriveAddressAsync(mnemonic, chain, accountIndex, i, true, ct))];

        await Task.WhenAll(externalTasks.Concat(internalTasks));

        List<AddressInfo> externalAddresses = [.. externalTasks.Select((t, i) => new AddressInfo { Index = i, Address = t.Result })];
        List<AddressInfo> internalAddresses = [.. internalTasks.Select((t, i) => new AddressInfo { Index = i, Address = t.Result })];

        // Cache addresses
        for (int i = 0; i < GapLimit; i++)
        {
            _sessionService.CacheAddress(wallet.Id, chain, accountIndex, i, false, externalAddresses[i].Address);
            _sessionService.CacheAddress(wallet.Id, chain, accountIndex, i, true, internalAddresses[i].Address);
        }

        ChainAddressData chainData = new()
        {
            Chain = chain,
            ExternalAddresses = externalAddresses,
            InternalAddresses = internalAddresses,
            LastSyncedAt = DateTime.UtcNow
        };

        WalletAccount? account = wallet.Accounts.FirstOrDefault(a => a.Index == accountIndex);
        if (account != null)
            account.ChainData[chain] = chainData;

        await _storage.SaveAsync(wallet, ct);
    }

    #endregion
}
