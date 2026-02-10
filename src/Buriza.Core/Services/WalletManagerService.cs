using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Transaction;
using Buriza.Core.Models.Wallet;
using Chrysalis.Wallet.Models.Keys;
using Transaction = Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction;

namespace Buriza.Core.Services;

public class WalletManagerService(
    BurizaStorageService storage,
    IBurizaChainProviderFactory providerFactory,
    IBurizaAppStateService appState) : IWalletManager, IDisposable
{
    private readonly BurizaStorageService _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    private readonly IBurizaChainProviderFactory _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
    private readonly IBurizaAppStateService _appState = appState ?? throw new ArgumentNullException(nameof(appState));
    private bool _disposed;

    #region Wallet Lifecycle

    public void GenerateMnemonic(int wordCount, Action<ReadOnlySpan<char>> onMnemonic)
    {
        byte[] mnemonicBytes = MnemonicService.Generate(wordCount);
        try
        {
            char[] mnemonicChars = Encoding.UTF8.GetChars(mnemonicBytes);
            try
            {
                onMnemonic(mnemonicChars);
            }
            finally
            {
                Array.Clear(mnemonicChars);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    public async Task<BurizaWallet> CreateFromMnemonicAsync(string name, string mnemonic, string password, ChainInfo? chainInfo = null, CancellationToken ct = default)
    {
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
        chainInfo ??= ChainRegistry.CardanoMainnet;

        if (!MnemonicService.Validate(mnemonicBytes))
            throw new ArgumentException("Invalid mnemonic", nameof(mnemonicBytes));

        Guid newId = Guid.NewGuid();

        // Derive only the first receive address (passing bytes directly)
        IKeyService keyService = _providerFactory.CreateKeyService(chainInfo);
        string receiveAddress = await keyService.DeriveAddressAsync(mnemonicBytes, chainInfo, 0, 0, false, ct);
        _appState.CacheAddress(newId, chainInfo, 0, 0, false, receiveAddress);

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
                new BurizaWalletAccount
                {
                    Index = 0,
                    Name = string.Format(DefaultAccountName, 1),
                    ChainData = new Dictionary<ChainType, ChainAddressData> { [chainInfo.Chain] = chainData }
                }
            ]
        };

        await _storage.CreateVaultAsync(wallet.Id, mnemonicBytes, password, ct);
        try
        {
            await _storage.SaveWalletAsync(wallet, ct);
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
        IReadOnlyList<BurizaWallet> wallets = await _storage.LoadAllWalletsAsync(ct);
        foreach (BurizaWallet wallet in wallets)
            AttachProvider(wallet);
        return wallets;
    }

    public async Task<BurizaWallet?> GetActiveAsync(CancellationToken ct = default)
    {
        Guid? activeId = await _storage.GetActiveWalletIdAsync(ct);
        if (!activeId.HasValue) return null;

        BurizaWallet? wallet = await _storage.LoadWalletAsync(activeId.Value, ct);
        if (wallet != null)
            AttachProvider(wallet);
        return wallet;
    }

    public async Task SetActiveAsync(Guid walletId, CancellationToken ct = default)
    {
        _ = await GetWalletOrThrowAsync(walletId, ct);
        await _storage.SetActiveWalletIdAsync(walletId, ct);
    }

    public async Task DeleteAsync(Guid walletId, CancellationToken ct = default)
    {
        // Verify wallet exists
        _ = await GetWalletOrThrowAsync(walletId, ct);

        // Clear session cache for this wallet
        _appState.ClearWalletCache(walletId);

        // Update active wallet if deleting the active one
        Guid? activeId = await _storage.GetActiveWalletIdAsync(ct);
        if (activeId == walletId)
        {
            IReadOnlyList<BurizaWallet> remaining = await _storage.LoadAllWalletsAsync(ct);
            BurizaWallet? next = remaining.FirstOrDefault(w => w.Id != walletId);
            if (next != null)
                await _storage.SetActiveWalletIdAsync(next.Id, ct);
            else
                await _storage.ClearActiveWalletIdAsync(ct);
        }

        await _storage.DeleteVaultAsync(walletId, ct);
        await _storage.DeleteWalletAsync(walletId, ct);
    }

    #endregion

    #region Chain Management

    public async Task SetActiveChainAsync(Guid walletId, ChainInfo chainInfo, string? password = null, CancellationToken ct = default)
    {
        // Validate chain is supported
        if (!GetAvailableChains().Contains(chainInfo.Chain))
            throw new NotSupportedException($"Chain {chainInfo.Chain} is not supported");

        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);

        BurizaWalletAccount? account = wallet.GetActiveAccount()
            ?? throw new InvalidOperationException("No active account");

        // Derive addresses for this chain if needed
        if (account.GetChainData(chainInfo.Chain) == null)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password is required to derive addresses for a new chain", nameof(password));

            byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, null, ct);
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
        await _storage.SaveWalletAsync(wallet, ct);

        AttachProvider(wallet);
    }

    public IReadOnlyList<ChainType> GetAvailableChains()
        => _providerFactory.GetSupportedChains();

    #endregion

    #region Account Management

    public async Task<BurizaWalletAccount> CreateAccountAsync(Guid walletId, string name, int? accountIndex = null, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);

        int index = accountIndex ?? (wallet.Accounts.Count > 0 ? wallet.Accounts.Max(a => a.Index) + 1 : 0);

        if (wallet.Accounts.Any(a => a.Index == index))
            throw new InvalidOperationException($"Account with index {index} already exists");

        BurizaWalletAccount account = new()
        {
            Index = index,
            Name = name
        };

        wallet.Accounts.Add(account);
        await _storage.SaveWalletAsync(wallet, ct);

        return account;
    }

    public async Task<BurizaWalletAccount?> GetActiveAccountAsync(CancellationToken ct = default)
    {
        BurizaWallet? wallet = await GetActiveAsync(ct);
        return wallet?.GetActiveAccount();
    }

    public async Task SetActiveAccountAsync(Guid walletId, int accountIndex, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        _ = GetAccountOrThrow(wallet, accountIndex);

        wallet.ActiveAccountIndex = accountIndex;
        wallet.LastAccessedAt = DateTime.UtcNow;
        await _storage.SaveWalletAsync(wallet, ct);
    }

    public async Task UnlockAccountAsync(Guid walletId, int accountIndex, string password, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        BurizaWalletAccount account = GetAccountOrThrow(wallet, accountIndex);
        ChainInfo chainInfo = GetChainInfo(wallet);
        ChainAddressData? existingData = account.GetChainData(chainInfo.Chain);

        // If chain data exists, just repopulate cache if needed
        if (existingData != null)
        {
            if (!_appState.HasCachedAddresses(walletId, chainInfo, accountIndex))
                _appState.CacheAddress(walletId, chainInfo, accountIndex, 0, false, existingData.ReceiveAddress);
            return;
        }

        // Derive new addresses
        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, null, ct);
        try
        {
            await DeriveAndSaveChainDataAsync(wallet, accountIndex, chainInfo, mnemonicBytes, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    public async Task RenameAccountAsync(Guid walletId, int accountIndex, string newName, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        BurizaWalletAccount account = GetAccountOrThrow(wallet, accountIndex);
        account.Name = newName;
        await _storage.SaveWalletAsync(wallet, ct);
    }

    private const int MaxAccountDiscoveryLimit = 100;
    private const string DefaultAccountName = "Account {0}";

    public async Task<IReadOnlyList<BurizaWalletAccount>> DiscoverAccountsAsync(Guid walletId, string password, int accountGapLimit = 5, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        ChainInfo chainInfo = GetChainInfo(wallet);
        IBurizaChainProvider provider = GetProviderForWallet(wallet);
        IKeyService keyService = _providerFactory.CreateKeyService(chainInfo);

        List<BurizaWalletAccount> discoveredAccounts = [];
        int consecutiveEmpty = 0;
        int accountIndex = 0;

        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, null, ct);
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
                string address = await keyService.DeriveAddressAsync(mnemonicBytes, chainInfo, accountIndex, 0, false, ct);

                // Check if address has any transaction history
                IReadOnlyList<TransactionHistory> history = await provider.GetTransactionHistoryAsync(address, 1, ct);

                if (history.Count > 0)
                {
                    // Account has been used - create it
                    BurizaWalletAccount account = new()
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
                    _appState.CacheAddress(walletId, chainInfo, accountIndex, 0, false, address);

                    consecutiveEmpty = 0;
                }
                else
                {
                    consecutiveEmpty++;
                }

                accountIndex++;
            }

            if (discoveredAccounts.Count > 0)
                await _storage.SaveWalletAsync(wallet, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }

        return discoveredAccounts;
    }

    #endregion

    #region Address Operations

    public async Task<string> GetReceiveAddressAsync(Guid walletId, int accountIndex = 0, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        ChainInfo chainInfo = GetChainInfo(wallet);

        // Try cache first
        string? cached = _appState.GetCachedAddress(walletId, chainInfo, accountIndex, 0, false);
        if (cached != null) return cached;

        // Otherwise get from stored wallet data
        BurizaWalletAccount account = GetAccountOrThrow(wallet, accountIndex);
        ChainAddressData chainData = account.GetChainData(chainInfo.Chain)
            ?? throw new InvalidOperationException($"Account {accountIndex} has no data for chain {chainInfo.Chain}. Call UnlockAccountAsync first.");

        return chainData.ReceiveAddress;
    }

    public async Task<string> GetStakingAddressAsync(Guid walletId, int accountIndex, string? password = null, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        ChainInfo chainInfo = GetChainInfo(wallet);
        BurizaWalletAccount account = GetAccountOrThrow(wallet, accountIndex);
        ChainAddressData? chainData = account.GetChainData(chainInfo.Chain);

        // Return cached staking address if available
        if (!string.IsNullOrEmpty(chainData?.StakingAddress))
            return chainData.StakingAddress;

        // Need password to derive
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password required to derive staking address", nameof(password));

        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, null, ct);
        try
        {
            IKeyService keyService = _providerFactory.CreateKeyService(chainInfo);
            string stakingAddress = await keyService.DeriveStakingAddressAsync(mnemonicBytes, chainInfo, accountIndex, ct);

            // Cache the staking address
            if (chainData != null)
            {
                chainData.StakingAddress = stakingAddress;
                await _storage.SaveWalletAsync(wallet, ct);
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

    public Task<bool> VerifyPasswordAsync(Guid walletId, string password, CancellationToken ct = default)
        => _storage.VerifyPasswordAsync(walletId, password, ct);

    public Task ChangePasswordAsync(Guid walletId, string currentPassword, string newPassword, CancellationToken ct = default)
        => _storage.ChangePasswordAsync(walletId, currentPassword, newPassword, ct);

    #endregion

    #region Sensitive Operations (Requires Password)

    public async Task<Transaction> SignTransactionAsync(Guid walletId, int accountIndex, int addressIndex, UnsignedTransaction unsignedTx, string password, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        ChainInfo chainInfo = GetChainInfo(wallet);
        IKeyService keyService = _providerFactory.CreateKeyService(chainInfo);
        PrivateKey? privateKey = null;
        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, null, ct);
        try
        {
            privateKey = await keyService.DerivePrivateKeyAsync(mnemonicBytes, chainInfo, accountIndex, addressIndex, ct: ct);
            object signedTx = wallet.Sign(unsignedTx, privateKey);
            return signedTx as Transaction ?? throw new InvalidOperationException("Failed to sign transaction");
        }
        finally
        {
            if (privateKey != null)
                CryptographicOperations.ZeroMemory(privateKey.Key);
            CryptographicOperations.ZeroMemory(mnemonicBytes);
        }
    }

    public async Task ExportMnemonicAsync(Guid walletId, string password, Action<ReadOnlySpan<char>> onMnemonic, CancellationToken ct = default)
    {
        await GetWalletOrThrowAsync(walletId, ct);
        byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password, null, ct);
        try
        {
            char[] mnemonicChars = Encoding.UTF8.GetChars(mnemonicBytes);
            try
            {
                onMnemonic(mnemonicChars);
            }
            finally
            {
                Array.Clear(mnemonicChars);
            }
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
        // Validate endpoint URL if provided - only allow HTTP/HTTPS schemes
        if (!string.IsNullOrEmpty(endpoint))
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
                throw new ArgumentException("Invalid endpoint URL", nameof(endpoint));

            if (uri.Scheme != "http" && uri.Scheme != "https")
                throw new ArgumentException("Endpoint must use HTTP or HTTPS", nameof(endpoint));
        }

        string? normalizedEndpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint;
        string? normalizedApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;

        // Save config metadata (endpoint, name)
        CustomProviderConfig config = new()
        {
            Chain = chainInfo.Chain,
            Network = chainInfo.Network,
            Endpoint = normalizedEndpoint,
            HasCustomApiKey = !string.IsNullOrEmpty(normalizedApiKey),
            Name = name
        };
        await _storage.SaveCustomProviderConfigAsync(config, ct);

        // Save encrypted API key if provided
        if (!string.IsNullOrEmpty(normalizedApiKey))
            await _storage.SaveCustomApiKeyAsync(chainInfo, normalizedApiKey, password, ct);
        else
            await _storage.DeleteCustomApiKeyAsync(chainInfo, ct);

        // Cache in session for immediate use
        _appState.SetChainConfig(chainInfo, new ServiceConfig(normalizedEndpoint, normalizedApiKey));
    }

    public async Task ClearCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        await _storage.DeleteCustomProviderConfigAsync(chainInfo, ct);
        _appState.ClearChainConfig(chainInfo);
    }

    public async Task LoadCustomProviderConfigAsync(ChainInfo chainInfo, string password, CancellationToken ct = default)
    {
        CustomProviderConfig? config = await _storage.GetCustomProviderConfigAsync(chainInfo, ct);
        if (config == null) return;

        string? endpoint = string.IsNullOrWhiteSpace(config.Endpoint) ? null : config.Endpoint;
        string? apiKey = null;
        if (config.HasCustomApiKey)
        {
            byte[]? apiKeyBytes = await _storage.UnlockCustomApiKeyAsync(chainInfo, password, ct);
            if (apiKeyBytes != null)
            {
                try
                {
                    apiKey = Encoding.UTF8.GetString(apiKeyBytes);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(apiKeyBytes);
                }
            }
        }

        _appState.SetChainConfig(chainInfo, new ServiceConfig(endpoint, apiKey));
    }

    #endregion

    #region Private Helpers

    private async Task<BurizaWallet> GetWalletOrThrowAsync(Guid walletId, CancellationToken ct)
    {
        BurizaWallet wallet = await _storage.LoadWalletAsync(walletId, ct)
            ?? throw new ArgumentException("Wallet not found", nameof(walletId));
        AttachProvider(wallet);
        return wallet;
    }

    private static BurizaWalletAccount GetAccountOrThrow(BurizaWallet wallet, int accountIndex)
        => wallet.Accounts.FirstOrDefault(a => a.Index == accountIndex)
            ?? throw new ArgumentException($"Account {accountIndex} not found", nameof(accountIndex));

    private void AttachProvider(BurizaWallet wallet)
        => wallet.Provider = GetProviderForWallet(wallet);

    private IBurizaChainProvider GetProviderForWallet(BurizaWallet wallet)
        => _providerFactory.CreateProvider(GetChainInfo(wallet));

    private static ChainInfo GetChainInfo(BurizaWallet wallet)
        => ChainRegistry.Get(wallet.ActiveChain, wallet.Network);

    private async Task DeriveAndSaveChainDataAsync(BurizaWallet wallet, int accountIndex, ChainInfo chainInfo, byte[] mnemonicBytes, CancellationToken ct)
    {
        IKeyService keyService = _providerFactory.CreateKeyService(chainInfo);
        string receiveAddress = await keyService.DeriveAddressAsync(mnemonicBytes, chainInfo, accountIndex, 0, false, ct);
        _appState.CacheAddress(wallet.Id, chainInfo, accountIndex, 0, false, receiveAddress);

        ChainAddressData chainData = new()
        {
            Chain = chainInfo.Chain,
            ReceiveAddress = receiveAddress,
            LastSyncedAt = DateTime.UtcNow
        };

        BurizaWalletAccount account = wallet.Accounts.FirstOrDefault(a => a.Index == accountIndex)
            ?? throw new InvalidOperationException($"Account {accountIndex} not found in wallet {wallet.Id}");
        account.ChainData[chainInfo.Chain] = chainData;

        await _storage.SaveWalletAsync(wallet, ct);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
