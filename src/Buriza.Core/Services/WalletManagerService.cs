using System.Security.Cryptography;
using System.Text;
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
    IChainProviderFactory providerFactory,
    IKeyService keyService,
    ISessionService sessionService) : IWalletManager, IDisposable
{
    private readonly IWalletStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    private readonly IChainProviderFactory _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
    private readonly IKeyService _keyService = keyService ?? throw new ArgumentNullException(nameof(keyService));
    private readonly ISessionService _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
    private bool _disposed;

    private const int GapLimit = 20;

    #region Wallet Lifecycle

    public async Task<BurizaWallet> CreateAsync(string name, string password, ChainType initialChain = ChainType.Cardano, NetworkType network = NetworkType.Mainnet, int mnemonicWordCount = 24, CancellationToken ct = default)
    {
        string mnemonic = _keyService.GenerateMnemonic(mnemonicWordCount);
        return await ImportAsync(name, mnemonic, password, initialChain, network, ct);
    }

    public async Task<BurizaWallet> ImportAsync(string name, string mnemonic, string password, ChainType initialChain = ChainType.Cardano, NetworkType network = NetworkType.Mainnet, CancellationToken ct = default)
    {
        if (!_keyService.ValidateMnemonic(mnemonic))
            throw new ArgumentException("Invalid mnemonic", nameof(mnemonic));

        int newId = await _storage.GenerateNextIdAsync(ct);

        Task<string>[] externalTasks = [.. Enumerable.Range(0, GapLimit).Select(i => _keyService.DeriveAddressAsync(mnemonic, initialChain, network, 0, i, false, ct))];
        Task<string>[] internalTasks = [.. Enumerable.Range(0, GapLimit).Select(i => _keyService.DeriveAddressAsync(mnemonic, initialChain, network, 0, i, true, ct))];

        await Task.WhenAll(externalTasks.Concat(internalTasks));

        List<AddressInfo> externalAddresses = [.. externalTasks.Select((t, i) => new AddressInfo { Index = i, Address = t.Result })];
        List<AddressInfo> internalAddresses = [.. internalTasks.Select((t, i) => new AddressInfo { Index = i, Address = t.Result })];

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
            Network = network,
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
        try
        {
            await _storage.SaveAsync(wallet, ct);
            await _storage.SetActiveWalletIdAsync(wallet.Id, ct);
        }
        catch
        {
            // Rollback vault on failure
            await _storage.DeleteVaultAsync(wallet.Id, ct);
            throw;
        }

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
        // Verify wallet exists
        _ = await GetWalletOrThrowAsync(walletId, ct);

        // Clear session cache for this wallet
        _sessionService.ClearWalletCache(walletId);

        // Update active wallet if deleting the active one
        int? activeId = await _storage.GetActiveWalletIdAsync(ct);
        if (activeId == walletId)
        {
            IReadOnlyList<BurizaWallet> remaining = await _storage.LoadAllAsync(ct);
            BurizaWallet? next = remaining.FirstOrDefault(w => w.Id != walletId);
            if (next != null)
                await _storage.SetActiveWalletIdAsync(next.Id, ct);
            else
                await _storage.ClearActiveWalletIdAsync(ct);
        }

        await _storage.DeleteVaultAsync(walletId, ct);
        await _storage.DeleteAsync(walletId, ct);
    }

    #endregion

    #region Chain Management

    public async Task SetActiveChainAsync(int walletId, ChainType chain, string? password = null, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);

        WalletAccount? account = wallet.GetActiveAccount()
            ?? throw new InvalidOperationException("No active account");

        // Derive addresses for this chain if needed
        if (account.GetChainData(chain) == null)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password is required to derive addresses for a new chain", nameof(password));

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

        AttachProvider(wallet);
    }

    public IReadOnlyList<ChainType> GetAvailableChains()
        => _providerFactory.GetSupportedChains();

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
        ChainAddressData? existingData = account.GetChainData(chain);

        // If chain data exists, just repopulate cache if needed
        if (existingData != null)
        {
            if (!_sessionService.HasCachedAddresses(walletId, chain, accountIndex))
            {
                foreach (AddressInfo addr in existingData.ExternalAddresses)
                    _sessionService.CacheAddress(walletId, chain, accountIndex, addr.Index, false, addr.Address);
                foreach (AddressInfo addr in existingData.InternalAddresses)
                    _sessionService.CacheAddress(walletId, chain, accountIndex, addr.Index, true, addr.Address);
            }
            return;
        }

        // Derive new addresses
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
        IChainProvider provider = GetProviderForWallet(wallet);
        PrivateKey? privateKey = null;
        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, ct);
        try
        {
            string mnemonic = Encoding.UTF8.GetString(mnemonicBytes);
            privateKey = await _keyService.DerivePrivateKeyAsync(mnemonic, wallet.ActiveChain, wallet.Network, accountIndex, addressIndex, ct: ct);
            return await provider.TransactionService.SignAsync(unsignedTx, privateKey, ct);
        }
        finally
        {
            if (privateKey != null)
                CryptographicOperations.ZeroMemory(privateKey.Key);
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    public async Task<byte[]> ExportMnemonicAsync(int walletId, string password, CancellationToken ct = default)
    {
        await GetWalletOrThrowAsync(walletId, ct);
        return await _storage.UnlockVaultAsync(walletId, password, ct);
    }

    #endregion

    #region Custom Provider Config

    public Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainType chain, NetworkType network, CancellationToken ct = default)
        => _storage.GetCustomProviderConfigAsync(chain, network, ct);

    public async Task SetCustomProviderConfigAsync(ChainType chain, NetworkType network, string? endpoint, string? apiKey, string password, string? name = null, CancellationToken ct = default)
    {
        // Validate endpoint URL if provided
        if (!string.IsNullOrEmpty(endpoint) && !Uri.TryCreate(endpoint, UriKind.Absolute, out _))
            throw new ArgumentException("Invalid endpoint URL", nameof(endpoint));

        // Save config metadata (endpoint, name)
        CustomProviderConfig config = new()
        {
            Chain = chain,
            Network = network,
            Endpoint = endpoint,
            HasCustomApiKey = !string.IsNullOrEmpty(apiKey),
            Name = name
        };
        await _storage.SaveCustomProviderConfigAsync(config, ct);

        // Cache endpoint in session for immediate use
        if (!string.IsNullOrEmpty(endpoint))
            _sessionService.SetCustomEndpoint(chain, network, endpoint);
        else
            _sessionService.ClearCustomEndpoint(chain, network);

        // Save encrypted API key if provided
        if (!string.IsNullOrEmpty(apiKey))
        {
            await _storage.SaveCustomApiKeyAsync(chain, network, apiKey, password, ct);
            // Also cache in session for immediate use
            _sessionService.SetCustomApiKey(chain, network, apiKey);
        }
        else
        {
            // Clear any existing API key
            await _storage.DeleteCustomApiKeyAsync(chain, network, ct);
            _sessionService.ClearCustomApiKey(chain, network);
        }
    }

    public async Task ClearCustomProviderConfigAsync(ChainType chain, NetworkType network, CancellationToken ct = default)
    {
        await _storage.DeleteCustomProviderConfigAsync(chain, network, ct);
        _sessionService.ClearCustomApiKey(chain, network);
        _sessionService.ClearCustomEndpoint(chain, network);
    }

    public async Task LoadCustomProviderConfigAsync(ChainType chain, NetworkType network, string password, CancellationToken ct = default)
    {
        // Load and cache endpoint (not encrypted)
        CustomProviderConfig? config = await _storage.GetCustomProviderConfigAsync(chain, network, ct);
        if (config?.Endpoint != null)
            _sessionService.SetCustomEndpoint(chain, network, config.Endpoint);

        // Load and cache API key (encrypted)
        if (config?.HasCustomApiKey == true)
        {
            byte[]? apiKeyBytes = await _storage.UnlockCustomApiKeyAsync(chain, network, password, ct);
            if (apiKeyBytes != null)
            {
                try
                {
                    string apiKey = Encoding.UTF8.GetString(apiKeyBytes);
                    _sessionService.SetCustomApiKey(chain, network, apiKey);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(apiKeyBytes);
                }
            }
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
        => wallet.Provider = GetProviderForWallet(wallet);

    private IChainProvider GetProviderForWallet(BurizaWallet wallet)
    {
        ProviderConfig config = _providerFactory.GetDefaultConfig(wallet.ActiveChain, wallet.Network);
        return _providerFactory.Create(config);
    }

    private async Task DeriveAndSaveChainDataAsync(BurizaWallet wallet, int accountIndex, ChainType chain, string mnemonic, CancellationToken ct)
    {
        Task<string>[] externalTasks = [.. Enumerable.Range(0, GapLimit).Select(i => _keyService.DeriveAddressAsync(mnemonic, chain, wallet.Network, accountIndex, i, false, ct))];
        Task<string>[] internalTasks = [.. Enumerable.Range(0, GapLimit).Select(i => _keyService.DeriveAddressAsync(mnemonic, chain, wallet.Network, accountIndex, i, true, ct))];

        await Task.WhenAll(externalTasks.Concat(internalTasks));

        List<AddressInfo> externalAddresses = [.. externalTasks.Select((t, i) => new AddressInfo { Index = i, Address = t.Result })];
        List<AddressInfo> internalAddresses = [.. internalTasks.Select((t, i) => new AddressInfo { Index = i, Address = t.Result })];

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
        account?.ChainData[chain] = chainData;

        await _storage.SaveAsync(wallet, ct);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _providerFactory.Dispose();
        _sessionService.Dispose();

        GC.SuppressFinalize(this);
    }

    #endregion
}
