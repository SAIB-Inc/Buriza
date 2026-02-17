using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Storage;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Transaction;
using Buriza.Core.Models.Wallet;
using Chrysalis.Wallet.Models.Keys;
using Transaction = Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction;


namespace Buriza.Core.Services;

/// <inheritdoc />
public class WalletManagerService(
    BurizaStorageBase storage,
    IBurizaChainProviderFactory providerFactory) : IWalletManager, IDisposable
{
    private readonly BurizaStorageBase _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    private readonly IBurizaChainProviderFactory _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
    private bool _disposed;

    #region Wallet Lifecycle

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task<BurizaWallet> CreateFromMnemonicAsync(string name, ReadOnlyMemory<byte> mnemonicBytes, ReadOnlyMemory<byte> passwordBytes, ChainInfo? chainInfo = null, CancellationToken ct = default)
    {
        byte[] mnemonicCopy = mnemonicBytes.ToArray();
        byte[] passwordCopy = passwordBytes.ToArray();
        try
        {
            return await ImportFromBytesAsync(name, mnemonicCopy, passwordCopy, chainInfo, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicCopy);
            CryptographicOperations.ZeroMemory(passwordCopy);
        }
    }

    private async Task<BurizaWallet> ImportFromBytesAsync(string name, byte[] mnemonicBytes, byte[] passwordBytes, ChainInfo? chainInfo, CancellationToken ct)
    {
        chainInfo ??= ChainRegistry.CardanoMainnet;

        if (!MnemonicService.Validate(mnemonicBytes))
            throw new ArgumentException("Invalid mnemonic", nameof(mnemonicBytes));

        Guid newId = Guid.NewGuid();

        IReadOnlyList<ChainInfo> initialChains = GetInitialChainInfos(chainInfo);
        Dictionary<ChainType, Dictionary<NetworkType, ChainAddressData>> chainDataByChain = [];

        foreach (ChainInfo info in initialChains)
        {
            IKeyService keyService = _providerFactory.CreateKeyService(info);
            ChainAddressData chainData = await keyService.DeriveChainDataAsync(mnemonicBytes, info, 0, 0, false, ct);
            chainData.LastSyncedAt = DateTime.UtcNow;

            if (!chainDataByChain.TryGetValue(info.Chain, out Dictionary<NetworkType, ChainAddressData>? perNetwork))
            {
                perNetwork = [];
                chainDataByChain[info.Chain] = perNetwork;
            }

            perNetwork[info.Network] = chainData;
        }

        BurizaWallet wallet = new(_providerFactory)
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
                    ChainData = chainDataByChain
                }
            ]
        };

        await _storage.CreateVaultAsync(wallet.Id, mnemonicBytes, passwordBytes, ct);
        try
        {
            await _storage.SaveWalletAsync(wallet, ct);
            await _storage.SetActiveWalletIdAsync(wallet.Id, ct);
        }
        catch
        {
            // Rollback vault and wallet metadata on failure
            await _storage.DeleteVaultAsync(wallet.Id, ct);
            await _storage.DeleteWalletAsync(wallet.Id, ct);
            throw;
        }

        return wallet;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BurizaWallet>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<BurizaWallet> wallets = await _storage.LoadAllWalletsAsync(ct);
        return BindRuntimeDependencies(wallets);
    }

    /// <inheritdoc/>
    public async Task<BurizaWallet?> GetActiveAsync(CancellationToken ct = default)
    {
        Guid? activeId = await _storage.GetActiveWalletIdAsync(ct);
        if (!activeId.HasValue) return null;

        BurizaWallet? wallet = await _storage.LoadWalletAsync(activeId.Value, ct);
        return wallet is null ? null : BindRuntimeDependencies(wallet);
    }

    /// <inheritdoc/>
    public async Task SetActiveAsync(Guid walletId, CancellationToken ct = default)
    {
        _ = await GetWalletOrThrowAsync(walletId, ct);
        await _storage.SetActiveWalletIdAsync(walletId, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid walletId, CancellationToken ct = default)
    {
        // Verify wallet exists
        _ = await GetWalletOrThrowAsync(walletId, ct);

        // Clear session cache for this wallet
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

    /// <inheritdoc/>
    public async Task SetActiveChainAsync(Guid walletId, ChainInfo chainInfo, ReadOnlyMemory<byte>? password = null, CancellationToken ct = default)
    {
        // Validate chain is supported
        if (!GetAvailableChains().Contains(chainInfo.Chain))
            throw new NotSupportedException($"Chain {chainInfo.Chain} is not supported");

        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);

        BurizaWalletAccount? account = wallet.GetActiveAccount()
            ?? throw new InvalidOperationException("No active account");

        // Derive addresses for this chain if needed
        if (account.GetChainData(chainInfo.Chain, chainInfo.Network) == null)
        {
            if (!password.HasValue || password.Value.IsEmpty)
                throw new ArgumentException("Password is required to derive addresses for a new chain", nameof(password));

            byte[] mnemonicBytes = await _storage.UnlockVaultAsync(walletId, password.Value, null, ct);
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
        wallet.Network = chainInfo.Network;
        wallet.LastAccessedAt = DateTime.UtcNow;
        await _storage.SaveWalletAsync(wallet, ct);

    }

    /// <inheritdoc/>
    public IReadOnlyList<ChainType> GetAvailableChains()
        => _providerFactory.GetSupportedChains();

    #endregion

    #region Account Management

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task<BurizaWalletAccount?> GetActiveAccountAsync(CancellationToken ct = default)
    {
        BurizaWallet? wallet = await GetActiveAsync(ct);
        return wallet?.GetActiveAccount();
    }

    /// <inheritdoc/>
    public async Task SetActiveAccountAsync(Guid walletId, int accountIndex, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        _ = GetAccountOrThrow(wallet, accountIndex);

        wallet.ActiveAccountIndex = accountIndex;
        wallet.LastAccessedAt = DateTime.UtcNow;
        await _storage.SaveWalletAsync(wallet, ct);
    }

    /// <inheritdoc/>
    public async Task UnlockAccountAsync(Guid walletId, int accountIndex, ReadOnlyMemory<byte> password, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        BurizaWalletAccount account = GetAccountOrThrow(wallet, accountIndex);
        ChainInfo chainInfo = GetChainInfo(wallet);
        ChainAddressData? existingData = account.GetChainData(chainInfo.Chain, chainInfo.Network);

        // If chain data exists, just repopulate cache if needed
        if (existingData != null)
            return;

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

    /// <inheritdoc/>
    public async Task RenameAccountAsync(Guid walletId, int accountIndex, string newName, CancellationToken ct = default)
    {
        BurizaWallet wallet = await GetWalletOrThrowAsync(walletId, ct);
        BurizaWalletAccount account = GetAccountOrThrow(wallet, accountIndex);
        account.Name = newName;
        await _storage.SaveWalletAsync(wallet, ct);
    }

    private const int MaxAccountDiscoveryLimit = 100;
    private const string DefaultAccountName = "Account {0}";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BurizaWalletAccount>> DiscoverAccountsAsync(Guid walletId, ReadOnlyMemory<byte> password, int accountGapLimit = 5, CancellationToken ct = default)
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
                ChainAddressData chainData = await keyService.DeriveChainDataAsync(mnemonicBytes, chainInfo, accountIndex, 0, false, ct);
                chainData.LastSyncedAt = DateTime.UtcNow;

                // Check if address has any UTxOs (indicates usage)
                bool used = await provider.IsAddressUsedAsync(chainData.Address, ct);

                if (used)
                {
                    // Account has been used - create it
                    BurizaWalletAccount account = new()
                    {
                        Index = accountIndex,
                        Name = string.Format(DefaultAccountName, accountIndex + 1)
                    };
                    if (!account.ChainData.TryGetValue(chainInfo.Chain, out Dictionary<NetworkType, ChainAddressData>? perNetwork))
                    {
                        perNetwork = [];
                        account.ChainData[chainInfo.Chain] = perNetwork;
                    }
                    perNetwork[chainInfo.Network] = chainData;

                    wallet.Accounts.Add(account);
                    discoveredAccounts.Add(account);
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

    #region Password Operations

    /// <inheritdoc/>
    public Task<bool> VerifyPasswordAsync(Guid walletId, ReadOnlyMemory<byte> password, CancellationToken ct = default)
        => _storage.VerifyPasswordAsync(walletId, password, ct);

    /// <inheritdoc/>
    public Task ChangePasswordAsync(Guid walletId, ReadOnlyMemory<byte> currentPassword, ReadOnlyMemory<byte> newPassword, CancellationToken ct = default)
        => _storage.ChangePasswordAsync(walletId, currentPassword, newPassword, ct);

    #endregion

    #region Sensitive Operations (Requires Password)

    /// <inheritdoc/>
    public async Task<Transaction> SignTransactionAsync(Guid walletId, int accountIndex, int addressIndex, UnsignedTransaction unsignedTx, ReadOnlyMemory<byte> password, CancellationToken ct = default)
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

    /// <inheritdoc/>
    public async Task ExportMnemonicAsync(Guid walletId, ReadOnlyMemory<byte> password, Action<ReadOnlySpan<char>> onMnemonic, CancellationToken ct = default)
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

    /// <inheritdoc/>
    public Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
        => _storage.GetCustomProviderConfigAsync(chainInfo, ct);

    /// <inheritdoc/>
    public async Task SetCustomProviderConfigAsync(ChainInfo chainInfo, string? endpoint, string? apiKey, ReadOnlyMemory<byte> password, string? name = null, CancellationToken ct = default)
    {
        string? normalizedEndpoint = ValidateAndNormalizeEndpoint(endpoint, nameof(endpoint));
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
        await _storage.SaveCustomProviderConfigAsync(config, normalizedApiKey, password, ct);

        // Cache in session for immediate use
        _providerFactory.SetChainConfig(chainInfo, new ServiceConfig(normalizedEndpoint, normalizedApiKey));
    }

    /// <inheritdoc/>
    public async Task ClearCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        await _storage.DeleteCustomProviderConfigAsync(chainInfo, ct);
        _providerFactory.ClearChainConfig(chainInfo);
    }

    /// <inheritdoc/>
    public async Task LoadCustomProviderConfigAsync(ChainInfo chainInfo, ReadOnlyMemory<byte> password, CancellationToken ct = default)
    {
        (CustomProviderConfig Config, string? ApiKey)? result =
            await _storage.GetCustomProviderConfigWithApiKeyAsync(chainInfo, password, ct);
        if (result is null) return;

        CustomProviderConfig config = result.Value.Config;
        string? endpoint;
        try
        {
            endpoint = ValidateAndNormalizeEndpoint(config.Endpoint, nameof(config.Endpoint));
        }
        catch (ArgumentException ex)
        {
            await _storage.DeleteCustomProviderConfigAsync(chainInfo, ct);
            _providerFactory.ClearChainConfig(chainInfo);
            throw new InvalidOperationException("Stored custom provider configuration is invalid and has been cleared.", ex);
        }

        _providerFactory.SetChainConfig(chainInfo, new ServiceConfig(endpoint, result.Value.ApiKey));
    }

    #endregion

    #region Private Helpers

    private async Task<BurizaWallet> GetWalletOrThrowAsync(Guid walletId, CancellationToken ct)
    {
        BurizaWallet wallet = await _storage.LoadWalletAsync(walletId, ct)
            ?? throw new ArgumentException("Wallet not found", nameof(walletId));
        return BindRuntimeDependencies(wallet);
    }

    private static BurizaWalletAccount GetAccountOrThrow(BurizaWallet wallet, int accountIndex)
        => wallet.Accounts.FirstOrDefault(a => a.Index == accountIndex)
            ?? throw new ArgumentException($"Account {accountIndex} not found", nameof(accountIndex));

    // Wallet metadata is persisted as JSON, but runtime services (like provider factory)
    // are not serializable. Rebind those dependencies after load before returning wallets.
    private BurizaWallet BindRuntimeDependencies(BurizaWallet wallet)
    {
        wallet.BindChainProviderFactory(_providerFactory);
        return wallet;
    }

    private IReadOnlyList<BurizaWallet> BindRuntimeDependencies(IReadOnlyList<BurizaWallet> wallets)
    {
        foreach (BurizaWallet wallet in wallets)
            wallet.BindChainProviderFactory(_providerFactory);
        return wallets;
    }

    private IBurizaChainProvider GetProviderForWallet(BurizaWallet wallet)
        => _providerFactory.CreateProvider(GetChainInfo(wallet));

    private static ChainInfo GetChainInfo(BurizaWallet wallet)
        => ChainRegistry.Get(wallet.ActiveChain, wallet.Network);

    private static IReadOnlyList<ChainInfo> GetInitialChainInfos(ChainInfo chainInfo)
    {
        if (chainInfo.Chain == ChainType.Cardano)
        {
            return
            [
                ChainRegistry.CardanoMainnet,
                ChainRegistry.CardanoPreview,
                ChainRegistry.CardanoPreprod
            ];
        }

        return [chainInfo];
    }

    private static string? ValidateAndNormalizeEndpoint(string? endpoint, string paramName)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return null;

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
            throw new ArgumentException("Invalid endpoint URL", paramName);

        if (uri.Scheme != "http" && uri.Scheme != "https")
            throw new ArgumentException("Endpoint must use HTTP or HTTPS", paramName);

        if (uri.Scheme == "http" && !uri.IsLoopback)
            throw new ArgumentException("HTTP endpoints are allowed only for localhost/loopback. Use HTTPS for remote providers.", paramName);

        return endpoint;
    }

    private async Task DeriveAndSaveChainDataAsync(BurizaWallet wallet, int accountIndex, ChainInfo chainInfo, byte[] mnemonicBytes, CancellationToken ct)
    {
        IKeyService keyService = _providerFactory.CreateKeyService(chainInfo);
        ChainAddressData chainData = await keyService.DeriveChainDataAsync(mnemonicBytes, chainInfo, accountIndex, 0, false, ct);
        chainData.LastSyncedAt = DateTime.UtcNow;

        BurizaWalletAccount account = wallet.Accounts.FirstOrDefault(a => a.Index == accountIndex)
            ?? throw new InvalidOperationException($"Account {accountIndex} not found in wallet {wallet.Id}");
        if (!account.ChainData.TryGetValue(chainInfo.Chain, out Dictionary<NetworkType, ChainAddressData>? perNetwork))
        {
            perNetwork = [];
            account.ChainData[chainInfo.Chain] = perNetwork;
        }
        perNetwork[chainInfo.Network] = chainData;

        await _storage.SaveWalletAsync(wallet, ct);
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
