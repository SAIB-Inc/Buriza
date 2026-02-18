using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Interfaces;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Transaction;
using Buriza.Core.Storage;
using System.Text.Json.Serialization;

namespace Buriza.Core.Models.Wallet;

/// <summary>
/// HD Wallet following BIP-39/BIP-44 standards.
/// One wallet = one mnemonic seed that can derive keys for multiple chains.
/// Chain-agnostic — all chain-specific logic lives in IChainWallet implementations.
/// </summary>
public class BurizaWallet(
    IBurizaChainProviderFactory? chainProviderFactory = null,
    BurizaStorageBase? storage = null) : IWallet
{
    private IBurizaChainProviderFactory? _chainProviderFactory = chainProviderFactory;
    private BurizaStorageBase? _storage = storage;

    [JsonIgnore]
    private byte[]? _mnemonicBytes;

    [JsonConstructor]
    public BurizaWallet() : this(null, null) { }

    public required Guid Id { get; init; }

    /// <summary>Wallet profile (name, label, avatar).</summary>
    public required WalletProfile Profile { get; set; }

    /// <summary>Network this wallet operates on (e.g. "mainnet", "preprod", "preview").</summary>
    public string Network { get; set; } = "mainnet";

    /// <summary>Current active chain for this wallet.</summary>
    public ChainType ActiveChain { get; set; } = ChainType.Cardano;

    /// <summary>Current active account index.</summary>
    public int ActiveAccountIndex { get; set; } = 0;

    /// <summary>All accounts in this wallet.</summary>
    public List<BurizaWalletAccount> Accounts { get; set; } = [];

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LastAccessedAt { get; set; }

    internal void BindRuntimeServices(IBurizaChainProviderFactory chainProviderFactory, BurizaStorageBase storage)
    {
        _chainProviderFactory = chainProviderFactory ?? throw new ArgumentNullException(nameof(chainProviderFactory));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    #region Lock / Unlock

    [JsonIgnore]
    public bool IsUnlocked => _mnemonicBytes is not null;

    public async Task UnlockAsync(ReadOnlyMemory<byte>? passwordOrPin = null, string? biometricReason = null, CancellationToken ct = default)
    {
        if (IsUnlocked) return;
        _mnemonicBytes = await EnsureStorage().UnlockVaultAsync(Id, passwordOrPin, biometricReason, ct);
    }

    public void Lock()
    {
        if (IsUnlocked)
        {
            CryptographicOperations.ZeroMemory(_mnemonicBytes);
            _mnemonicBytes = null;
        }
    }

    #endregion

    #region Account & Address Helpers

    public BurizaWalletAccount? GetActiveAccount() =>
        Accounts.FirstOrDefault(a => a.Index == ActiveAccountIndex);

    public async Task<ChainAddressData?> GetAddressInfoAsync(int? accountIndex = null, CancellationToken ct = default)
    {
        if (!IsUnlocked)
            return null;

        BurizaWalletAccount? account = accountIndex.HasValue
            ? Accounts.FirstOrDefault(a => a.Index == accountIndex.Value)
            : GetActiveAccount();

        if (account is null)
            return null;

        ChainInfo chainInfo = ChainRegistry.Get(ActiveChain, Network);
        IChainWallet chainWallet = EnsureFactory().CreateChainWallet(chainInfo);
        return await chainWallet.DeriveChainDataAsync(_mnemonicBytes, chainInfo, account.Index, 0, false, ct);
    }

    /// <summary>Gets the chain wallet for the active chain.</summary>
    [JsonIgnore]
    public IChainWallet ChainWallet
    {
        get
        {
            ChainInfo chainInfo = ChainRegistry.Get(ActiveChain, Network);
            return EnsureFactory().CreateChainWallet(chainInfo);
        }
    }

    #endregion

    #region IWallet - Query Operations

    public async Task<ulong> GetBalanceAsync(int? accountIndex = null, CancellationToken ct = default)
    {
        using IBurizaChainProvider provider = EnsureProvider();

        string? address = (await GetAddressInfoAsync(accountIndex, ct))?.Address;
        if (string.IsNullOrEmpty(address)) return 0;

        return await provider.GetBalanceAsync(address, ct);
    }

    public async Task<IReadOnlyList<Asset>> GetAssetsAsync(int? accountIndex = null, CancellationToken ct = default)
    {
        using IBurizaChainProvider provider = EnsureProvider();

        string? address = (await GetAddressInfoAsync(accountIndex, ct))?.Address;
        if (string.IsNullOrEmpty(address)) return [];

        return await provider.GetAssetsAsync(address, ct);
    }

    public async Task<IReadOnlyList<Utxo>> GetUtxosAsync(int? accountIndex = null, CancellationToken ct = default)
    {
        using IBurizaChainProvider provider = EnsureProvider();

        string? address = (await GetAddressInfoAsync(accountIndex, ct))?.Address;
        if (string.IsNullOrEmpty(address)) return [];

        return await provider.GetUtxosAsync(address, ct);
    }

    #endregion

    #region Account Management

    private const int MaxAccountDiscoveryLimit = 100;
    private const string DefaultAccountName = "Account {0}";

    public async Task<BurizaWalletAccount> CreateAccountAsync(string name, int? accountIndex = null, CancellationToken ct = default)
    {
        int index = accountIndex ?? (Accounts.Count > 0 ? Accounts.Max(a => a.Index) + 1 : 0);

        if (Accounts.Any(a => a.Index == index))
            throw new InvalidOperationException($"Account with index {index} already exists");

        BurizaWalletAccount account = new()
        {
            Index = index,
            Name = name
        };

        Accounts.Add(account);
        await SaveAsync(ct);

        return account;
    }

    public async Task SetActiveAccountAsync(int accountIndex, CancellationToken ct = default)
    {
        _ = Accounts.FirstOrDefault(a => a.Index == accountIndex)
            ?? throw new ArgumentException($"Account {accountIndex} not found", nameof(accountIndex));

        ActiveAccountIndex = accountIndex;
        LastAccessedAt = DateTime.UtcNow;
        await SaveAsync(ct);
    }

    public async Task UpdateAccountAsync(int accountIndex, string? name = null, string? avatar = null, CancellationToken ct = default)
    {
        BurizaWalletAccount account = Accounts.FirstOrDefault(a => a.Index == accountIndex)
            ?? throw new ArgumentException($"Account {accountIndex} not found", nameof(accountIndex));

        if (name is not null) account.Name = name;
        if (avatar is not null) account.Avatar = avatar;

        await SaveAsync(ct);
    }

    public async Task<IReadOnlyList<BurizaWalletAccount>> DiscoverAccountsAsync(int accountGapLimit = 5, CancellationToken ct = default)
    {
        if (!IsUnlocked)
            throw new InvalidOperationException("Wallet must be unlocked to discover accounts.");

        ChainInfo chainInfo = ChainRegistry.Get(ActiveChain, Network);
        using IBurizaChainProvider provider = EnsureProvider();
        IChainWallet chainWallet = ChainWallet;

        List<BurizaWalletAccount> discoveredAccounts = [];
        int consecutiveEmpty = 0;
        int accountIndex = 0;

        while (consecutiveEmpty < accountGapLimit && accountIndex < MaxAccountDiscoveryLimit)
        {
            if (Accounts.Any(a => a.Index == accountIndex))
            {
                consecutiveEmpty = 0;
                accountIndex++;
                continue;
            }

            ChainAddressData chainData = await chainWallet.DeriveChainDataAsync(_mnemonicBytes, chainInfo, accountIndex, 0, false, ct);
            bool used = await provider.IsAddressUsedAsync(chainData.Address, ct);

            if (used)
            {
                BurizaWalletAccount account = new()
                {
                    Index = accountIndex,
                    Name = string.Format(DefaultAccountName, accountIndex + 1)
                };

                Accounts.Add(account);
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
            await SaveAsync(ct);

        return discoveredAccounts;
    }

    #endregion

    #region Chain Management

    public async Task SetActiveChainAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        if (!EnsureFactory().GetSupportedChains().Contains(chainInfo.Chain))
            throw new NotSupportedException($"Chain {chainInfo.Chain} is not supported");

        ActiveChain = chainInfo.Chain;
        Network = chainInfo.Network;
        LastAccessedAt = DateTime.UtcNow;
        await SaveAsync(ct);
    }

    #endregion

    #region Password Operations

    public Task<bool> VerifyPasswordAsync(ReadOnlyMemory<byte> password, CancellationToken ct = default)
        => EnsureStorage().VerifyPasswordAsync(Id, password, ct);

    public Task ChangePasswordAsync(ReadOnlyMemory<byte> currentPassword, ReadOnlyMemory<byte> newPassword, CancellationToken ct = default)
        => EnsureStorage().ChangePasswordAsync(Id, currentPassword, newPassword, ct);

    #endregion

    #region Sensitive Operations

    public void ExportMnemonic(Action<ReadOnlySpan<char>> onMnemonic)
    {
        if (!IsUnlocked)
            throw new InvalidOperationException("Wallet must be unlocked to export mnemonic.");

        char[] mnemonicChars = Encoding.UTF8.GetChars(_mnemonicBytes!);
        try
        {
            onMnemonic(mnemonicChars);
        }
        finally
        {
            Array.Clear(mnemonicChars);
        }
    }

    #endregion

    #region Custom Provider Config

    public Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
        => EnsureStorage().GetCustomProviderConfigAsync(chainInfo, ct);

    public async Task SetCustomProviderConfigAsync(ChainInfo chainInfo, string? endpoint, string? apiKey, ReadOnlyMemory<byte> password, string? name = null, CancellationToken ct = default)
    {
        string? normalizedEndpoint = ValidateAndNormalizeEndpoint(endpoint);
        string? normalizedApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;

        CustomProviderConfig config = new()
        {
            Chain = chainInfo.Chain,
            Network = chainInfo.Network,
            Endpoint = normalizedEndpoint,
            HasCustomApiKey = !string.IsNullOrEmpty(normalizedApiKey),
            Name = name
        };
        await EnsureStorage().SaveCustomProviderConfigAsync(config, normalizedApiKey, password, ct);

        EnsureFactory().SetChainConfig(chainInfo, new ServiceConfig(normalizedEndpoint, normalizedApiKey));
    }

    public async Task ClearCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        await EnsureStorage().DeleteCustomProviderConfigAsync(chainInfo, ct);
        EnsureFactory().ClearChainConfig(chainInfo);
    }

    public async Task LoadCustomProviderConfigAsync(ChainInfo chainInfo, ReadOnlyMemory<byte> password, CancellationToken ct = default)
    {
        (CustomProviderConfig Config, string? ApiKey)? result =
            await EnsureStorage().GetCustomProviderConfigWithApiKeyAsync(chainInfo, password, ct);
        if (result is null) return;

        CustomProviderConfig config = result.Value.Config;
        string? endpoint;
        try
        {
            endpoint = ValidateAndNormalizeEndpoint(config.Endpoint);
        }
        catch (ArgumentException ex)
        {
            await EnsureStorage().DeleteCustomProviderConfigAsync(chainInfo, ct);
            EnsureFactory().ClearChainConfig(chainInfo);
            throw new InvalidOperationException("Stored custom provider configuration is invalid and has been cleared.", ex);
        }

        EnsureFactory().SetChainConfig(chainInfo, new ServiceConfig(endpoint, result.Value.ApiKey));
    }

    private static string? ValidateAndNormalizeEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return null;

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
            throw new ArgumentException("Invalid endpoint URL", nameof(endpoint));

        if (uri.Scheme != "http" && uri.Scheme != "https")
            throw new ArgumentException("Endpoint must use HTTP or HTTPS", nameof(endpoint));

        if (uri.Scheme == "http" && !uri.IsLoopback)
            throw new ArgumentException("HTTP endpoints are allowed only for localhost/loopback. Use HTTPS for remote providers.", nameof(endpoint));

        return endpoint;
    }

    #endregion

    #region Persistence

    public Task SaveAsync(CancellationToken ct = default)
        => EnsureStorage().SaveWalletAsync(this, ct);

    #endregion

    #region Transaction Operations

    /// <summary>Builds, signs, and submits a transaction. Wallet must be unlocked.</summary>
    public async Task<string> SendAsync(TransactionRequest request, CancellationToken ct = default)
    {
        if (!IsUnlocked)
            throw new InvalidOperationException("Wallet must be unlocked to send transactions.");

        string fromAddress = (await GetAddressInfoAsync(ct: ct))?.Address
            ?? throw new InvalidOperationException("No receive address available for the active account.");

        using IBurizaChainProvider provider = EnsureProvider();
        ChainInfo chainInfo = ChainRegistry.Get(ActiveChain, Network);
        IChainWallet chainWallet = ChainWallet;

        UnsignedTransaction unsignedTx = await chainWallet.BuildTransactionAsync(fromAddress, request, provider, ct);
        byte[] signedTxBytes = chainWallet.Sign(unsignedTx, _mnemonicBytes!, chainInfo, ActiveAccountIndex, 0);
        return await provider.SubmitAsync(signedTxBytes, ct);
    }

    #endregion

    private BurizaStorageBase EnsureStorage()
        => _storage ?? throw new InvalidOperationException("Wallet storage is not bound. Use WalletManager to load the wallet.");

    private IBurizaChainProviderFactory EnsureFactory()
        => _chainProviderFactory ?? throw new InvalidOperationException("Wallet is not connected to a chain provider factory. Use WalletManager to load the wallet.");

    private IBurizaChainProvider EnsureProvider()
    {
        ChainInfo chainInfo = ChainRegistry.Get(ActiveChain, Network);
        return EnsureFactory().CreateProvider(chainInfo);
    }
}
