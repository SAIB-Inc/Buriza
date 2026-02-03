using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Models;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Chrysalis.Wallet.Models.Keys;
using Transaction = Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction;
using ChainRegistryData = Buriza.Data.Models.Common.ChainRegistry;
using WalletAccount = Buriza.Core.Models.WalletAccount;

namespace Buriza.Core.Services;

public class WalletManagerService(
    IWalletStorage storage,
    IChainRegistry chainRegistry,
    IKeyService keyService,
    ISessionService sessionService) : IWalletManager, IDisposable
{
    private readonly IWalletStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    private readonly IChainRegistry _chainRegistry = chainRegistry ?? throw new ArgumentNullException(nameof(chainRegistry));
    private readonly IKeyService _keyService = keyService ?? throw new ArgumentNullException(nameof(keyService));
    private readonly ISessionService _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
    private bool _disposed;

    #region Wallet Lifecycle

    public async Task<BurizaWallet> CreateAsync(string name, string password, ChainInfo? chainInfo = null, int mnemonicWordCount = 24, CancellationToken ct = default)
    {
        byte[] mnemonicBytes = _keyService.GenerateMnemonic(mnemonicWordCount);
        try
        {
            return await ImportFromBytesAsync(name, mnemonicBytes, password, chainInfo, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    public async Task<BurizaWallet> ImportAsync(string name, string mnemonic, string password, ChainInfo? chainInfo = null, CancellationToken ct = default)
    {
        // Convert user-provided string to bytes, then delegate to bytes-based import
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic);
        try
        {
            return await ImportFromBytesAsync(name, mnemonicBytes, password, chainInfo, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    private async Task<BurizaWallet> ImportFromBytesAsync(string name, byte[] mnemonicBytes, string password, ChainInfo? chainInfo, CancellationToken ct)
    {
        chainInfo ??= ChainRegistryData.CardanoMainnet;

        if (!_keyService.ValidateMnemonic(mnemonicBytes))
            throw new ArgumentException("Invalid mnemonic", nameof(mnemonicBytes));

        int newId = await _storage.GenerateNextIdAsync(ct);

        // Derive only the first receive address (passing bytes directly)
        string receiveAddress = await _keyService.DeriveAddressAsync(mnemonicBytes, chainInfo, 0, 0, false, ct);
        _sessionService.CacheAddress(newId, chainInfo, 0, 0, false, receiveAddress);

        ChainAddressData chainData = new()
        {
            Chain = chainInfo.Chain,
            ReceiveAddress = receiveAddress,
            LastSyncedAt = DateTime.UtcNow
        };

        BurizaWallet wallet = new()
        {
            Id = newId,
            Profile = new WalletProfile { Name = name },
            Network = chainInfo.Network,
            ActiveChain = chainInfo.Chain,
            ActiveAccountIndex = 0,
            Accounts =
            [
                new WalletAccount
                {
                    Index = 0,
                    Name = string.Format(DefaultAccountName, 1),
                    ChainData = new Dictionary<ChainType, ChainAddressData> { [chainInfo.Chain] = chainData }
                }
            ]
        };

        // Storage needs string for JSON serialization - convert only at storage boundary
        string mnemonicStr = Encoding.UTF8.GetString(mnemonicBytes);
        await _storage.CreateVaultAsync(wallet.Id, mnemonicStr, password, ct);
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

    public async Task SetActiveChainAsync(int walletId, ChainInfo chainInfo, string? password = null, CancellationToken ct = default)
    {
        // Validate chain is supported
        if (!GetAvailableChains().Contains(chainInfo.Chain))
            throw new NotSupportedException($"Chain {chainInfo.Chain} is not supported");

        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);

        WalletAccount? account = wallet.GetActiveAccount()
            ?? throw new InvalidOperationException("No active account");

        // Derive addresses for this chain if needed
        if (account.GetChainData(chainInfo.Chain) == null)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password is required to derive addresses for a new chain", nameof(password));

            byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, ct);
            try
            {
                await DeriveAndSaveChainDataAsync(wallet, account.Index, chainInfo, mnemonicBytes, ct);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(mnemonicBytes);
            }
        }

        wallet.ActiveChain = chainInfo.Chain;
        wallet.LastAccessedAt = DateTime.UtcNow;
        await _storage.SaveAsync(wallet, ct);

        AttachProvider(wallet);
    }

    public IReadOnlyList<ChainType> GetAvailableChains()
        => _chainRegistry.GetSupportedChains();

    #endregion

    #region Account Management

    public async Task<WalletAccount> CreateAccountAsync(int walletId, string name, int? accountIndex = null, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);

        int index = accountIndex ?? (wallet.Accounts.Count > 0 ? wallet.Accounts.Max(a => a.Index) + 1 : 0);

        if (wallet.Accounts.Any(a => a.Index == index))
            throw new InvalidOperationException($"Account with index {index} already exists");

        WalletAccount account = new()
        {
            Index = index,
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
        _ = GetAccountOrThrow(wallet, accountIndex);

        wallet.ActiveAccountIndex = accountIndex;
        wallet.LastAccessedAt = DateTime.UtcNow;
        await _storage.SaveAsync(wallet, ct);
    }

    public async Task UnlockAccountAsync(int walletId, int accountIndex, string password, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        WalletAccount account = GetAccountOrThrow(wallet, accountIndex);
        ChainInfo chainInfo = GetChainInfo(wallet);
        ChainAddressData? existingData = account.GetChainData(chainInfo.Chain);

        // If chain data exists, just repopulate cache if needed
        if (existingData != null)
        {
            if (!_sessionService.HasCachedAddresses(walletId, chainInfo, accountIndex))
                _sessionService.CacheAddress(walletId, chainInfo, accountIndex, 0, false, existingData.ReceiveAddress);
            return;
        }

        // Derive new addresses
        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, ct);
        try
        {
            await DeriveAndSaveChainDataAsync(wallet, accountIndex, chainInfo, mnemonicBytes, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    public async Task RenameAccountAsync(int walletId, int accountIndex, string newName, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        WalletAccount account = GetAccountOrThrow(wallet, accountIndex);
        account.Name = newName;
        await _storage.SaveAsync(wallet, ct);
    }

    private const int MaxAccountDiscoveryLimit = 100;
    private const string DefaultAccountName = "Account {0}";

    public async Task<IReadOnlyList<WalletAccount>> DiscoverAccountsAsync(int walletId, string password, int accountGapLimit = 5, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        ChainInfo chainInfo = GetChainInfo(wallet);
        IChainProvider provider = GetProviderForWallet(wallet);

        List<WalletAccount> discoveredAccounts = [];
        int consecutiveEmpty = 0;
        int accountIndex = 0;

        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, ct);
        try
        {
            while (consecutiveEmpty < accountGapLimit && accountIndex < MaxAccountDiscoveryLimit)
            {
                // Skip if account already exists
                if (wallet.Accounts.Any(a => a.Index == accountIndex))
                {
                    consecutiveEmpty = 0;
                    accountIndex++;
                    continue;
                }

                // Derive first receive address for this account (passing bytes directly)
                string address = await _keyService.DeriveAddressAsync(mnemonicBytes, chainInfo, accountIndex, 0, false, ct);

                // Check if address has any transaction history
                IReadOnlyList<TransactionHistory> history = await provider.QueryService.GetTransactionHistoryAsync(address, 1, ct);

                if (history.Count > 0)
                {
                    // Account has been used - create it
                    WalletAccount account = new()
                    {
                        Index = accountIndex,
                        Name = string.Format(DefaultAccountName, accountIndex + 1)
                    };

                    ChainAddressData chainData = new()
                    {
                        Chain = chainInfo.Chain,
                        ReceiveAddress = address,
                        LastSyncedAt = DateTime.UtcNow
                    };
                    account.ChainData[chainInfo.Chain] = chainData;

                    wallet.Accounts.Add(account);
                    discoveredAccounts.Add(account);
                    _sessionService.CacheAddress(walletId, chainInfo, accountIndex, 0, false, address);

                    consecutiveEmpty = 0;
                }
                else
                {
                    consecutiveEmpty++;
                }

                accountIndex++;
            }

            if (discoveredAccounts.Count > 0)
                await _storage.SaveAsync(wallet, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }

        return discoveredAccounts;
    }

    #endregion

    #region Address Operations

    public async Task<string> GetReceiveAddressAsync(int walletId, int accountIndex = 0, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        ChainInfo chainInfo = GetChainInfo(wallet);

        // Try cache first
        string? cached = _sessionService.GetCachedAddress(walletId, chainInfo, accountIndex, 0, false);
        if (cached != null) return cached;

        // Otherwise get from stored wallet data
        WalletAccount account = GetAccountOrThrow(wallet, accountIndex);
        ChainAddressData chainData = account.GetChainData(chainInfo.Chain)
            ?? throw new InvalidOperationException($"Account {accountIndex} has no data for chain {chainInfo.Chain}. Call UnlockAccountAsync first.");

        return chainData.ReceiveAddress;
    }

    public async Task<string> GetStakingAddressAsync(int walletId, int accountIndex, string? password = null, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        ChainInfo chainInfo = GetChainInfo(wallet);
        WalletAccount account = GetAccountOrThrow(wallet, accountIndex);
        ChainAddressData? chainData = account.GetChainData(chainInfo.Chain);

        // Return cached staking address if available
        if (!string.IsNullOrEmpty(chainData?.StakingAddress))
            return chainData.StakingAddress;

        // Need password to derive
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password required to derive staking address", nameof(password));

        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, ct);
        try
        {
            string stakingAddress = await _keyService.DeriveStakingAddressAsync(mnemonicBytes, chainInfo, accountIndex, ct);

            // Cache the staking address
            if (chainData != null)
            {
                chainData.StakingAddress = stakingAddress;
                await _storage.SaveAsync(wallet, ct);
            }

            return stakingAddress;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    #endregion

    #region Password Operations

    public Task<bool> VerifyPasswordAsync(int walletId, string password, CancellationToken ct = default)
        => _storage.VerifyPasswordAsync(walletId, password, ct);

    public Task ChangePasswordAsync(int walletId, string currentPassword, string newPassword, CancellationToken ct = default)
        => _storage.ChangePasswordAsync(walletId, currentPassword, newPassword, ct);

    #endregion

    #region Sensitive Operations (Requires Password)

    public async Task<Transaction> SignTransactionAsync(int walletId, int accountIndex, int addressIndex, UnsignedTransaction unsignedTx, string password, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        IChainProvider provider = GetProviderForWallet(wallet);
        ChainInfo chainInfo = GetChainInfo(wallet);
        PrivateKey? privateKey = null;
        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, ct);
        try
        {
            privateKey = await _keyService.DerivePrivateKeyAsync(mnemonicBytes, chainInfo, accountIndex, addressIndex, ct: ct);
            return await provider.TransactionService.SignAsync(unsignedTx, privateKey, ct);
        }
        finally
        {
            if (privateKey != null)
                CryptographicOperations.ZeroMemory(privateKey.Key);
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    public async Task ExportMnemonicAsync(int walletId, string password, Action<ReadOnlySpan<char>> onMnemonic, CancellationToken ct = default)
    {
        await GetWalletOrThrowAsync(walletId, ct);
        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, ct);
        try
        {
            onMnemonic(Encoding.UTF8.GetString(mnemonicBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    #endregion

    #region Custom Provider Config

    public Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
        => _storage.GetCustomProviderConfigAsync(chainInfo, ct);

    public async Task SetCustomProviderConfigAsync(ChainInfo chainInfo, string? endpoint, string? apiKey, string password, string? name = null, CancellationToken ct = default)
    {
        // Validate endpoint URL if provided
        if (!string.IsNullOrEmpty(endpoint) && !Uri.TryCreate(endpoint, UriKind.Absolute, out _))
            throw new ArgumentException("Invalid endpoint URL", nameof(endpoint));

        // Save config metadata (endpoint, name)
        CustomProviderConfig config = new()
        {
            Chain = chainInfo.Chain,
            Network = chainInfo.Network,
            Endpoint = endpoint,
            HasCustomApiKey = !string.IsNullOrEmpty(apiKey),
            Name = name
        };
        await _storage.SaveCustomProviderConfigAsync(config, ct);

        // Cache endpoint in session for immediate use
        if (!string.IsNullOrEmpty(endpoint))
            _sessionService.SetCustomEndpoint(chainInfo, endpoint);
        else
            _sessionService.ClearCustomEndpoint(chainInfo);

        // Save encrypted API key if provided
        if (!string.IsNullOrEmpty(apiKey))
        {
            await _storage.SaveCustomApiKeyAsync(chainInfo, apiKey, password, ct);
            // Also cache in session for immediate use
            _sessionService.SetCustomApiKey(chainInfo, apiKey);
        }
        else
        {
            // Clear any existing API key
            await _storage.DeleteCustomApiKeyAsync(chainInfo, ct);
            _sessionService.ClearCustomApiKey(chainInfo);
        }

        // Invalidate cached provider so next request uses new config
        _chainRegistry.InvalidateCache(chainInfo);
    }

    public async Task ClearCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        await _storage.DeleteCustomProviderConfigAsync(chainInfo, ct);
        _sessionService.ClearCustomApiKey(chainInfo);
        _sessionService.ClearCustomEndpoint(chainInfo);

        // Invalidate cached provider so next request uses default config
        _chainRegistry.InvalidateCache(chainInfo);
    }

    public async Task LoadCustomProviderConfigAsync(ChainInfo chainInfo, string password, CancellationToken ct = default)
    {
        // Load and cache endpoint (not encrypted)
        CustomProviderConfig? config = await _storage.GetCustomProviderConfigAsync(chainInfo, ct);
        if (config?.Endpoint != null)
            _sessionService.SetCustomEndpoint(chainInfo, config.Endpoint);

        // Load and cache API key (encrypted)
        if (config?.HasCustomApiKey == true)
        {
            byte[]? apiKeyBytes = await _storage.UnlockCustomApiKeyAsync(chainInfo, password, ct);
            if (apiKeyBytes != null)
            {
                try
                {
                    string apiKey = Encoding.UTF8.GetString(apiKeyBytes);
                    _sessionService.SetCustomApiKey(chainInfo, apiKey);
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

    private static WalletAccount GetAccountOrThrow(BurizaWallet wallet, int accountIndex)
        => wallet.Accounts.FirstOrDefault(a => a.Index == accountIndex)
            ?? throw new ArgumentException($"Account {accountIndex} not found", nameof(accountIndex));

    private void AttachProvider(BurizaWallet wallet)
        => wallet.Provider = GetProviderForWallet(wallet);

    private IChainProvider GetProviderForWallet(BurizaWallet wallet)
        => _chainRegistry.GetProvider(GetChainInfo(wallet));

    private static ChainInfo GetChainInfo(BurizaWallet wallet)
        => ChainRegistryData.Get(wallet.ActiveChain, wallet.Network);

    private async Task DeriveAndSaveChainDataAsync(BurizaWallet wallet, int accountIndex, ChainInfo chainInfo, byte[] mnemonicBytes, CancellationToken ct)
    {
        string receiveAddress = await _keyService.DeriveAddressAsync(mnemonicBytes, chainInfo, accountIndex, 0, false, ct);
        _sessionService.CacheAddress(wallet.Id, chainInfo, accountIndex, 0, false, receiveAddress);

        ChainAddressData chainData = new()
        {
            Chain = chainInfo.Chain,
            ReceiveAddress = receiveAddress,
            LastSyncedAt = DateTime.UtcNow
        };

        WalletAccount account = wallet.Accounts.FirstOrDefault(a => a.Index == accountIndex)
            ?? throw new InvalidOperationException($"Account {accountIndex} not found in wallet {wallet.Id}");
        account.ChainData[chainInfo.Chain] = chainData;

        await _storage.SaveAsync(wallet, ct);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Note: Do NOT dispose _chainRegistry or _sessionService here.
        // They are injected dependencies (typically singletons) and their
        // lifecycle is managed by the DI container, not by this service.

        GC.SuppressFinalize(this);
    }

    #endregion
}
