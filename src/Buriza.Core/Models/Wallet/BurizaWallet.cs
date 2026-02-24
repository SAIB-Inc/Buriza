using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Interfaces;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Transaction;
using Buriza.Core.Storage;
using System.Text.Json.Serialization;

namespace Buriza.Core.Models.Wallet;

/// <summary>
/// HD Wallet following CIP-1852 standards.
/// One wallet = one mnemonic seed that can derive keys for multiple chains.
/// Chain-agnostic — all chain-specific logic lives in IChainWallet implementations.
/// </summary>
public class BurizaWallet(
    IBurizaChainProviderFactory? chainProviderFactory = null,
    BurizaStorageBase? storage = null) : IDisposable
{
    private IBurizaChainProviderFactory? _chainProviderFactory = chainProviderFactory;
    private BurizaStorageBase? _storage = storage;

    [JsonIgnore]
    private byte[]? _mnemonicBytes;

    internal BurizaWallet(
        IBurizaChainProviderFactory chainProviderFactory,
        BurizaStorageBase storage,
        ReadOnlyMemory<byte> mnemonicBytes) : this(chainProviderFactory, storage)
    {
        _mnemonicBytes = mnemonicBytes.ToArray();
    }

    [JsonConstructor]
    public BurizaWallet() : this(null, null) { }

    /// <summary>Unique wallet identifier.</summary>
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

    /// <summary>Timestamp of last user interaction with this wallet.</summary>
    public DateTime? LastAccessedAt { get; set; }

    internal void Attach(IBurizaChainProviderFactory chainProviderFactory, BurizaStorageBase storage)
    {
        _chainProviderFactory = chainProviderFactory ?? throw new ArgumentNullException(nameof(chainProviderFactory));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    #region Lock / Unlock

    /// <summary>Indicates whether the mnemonic is currently decrypted in memory.</summary>
    [JsonIgnore]
    public bool IsUnlocked => _mnemonicBytes is not null;

    /// <summary>Decrypts the vault and loads the mnemonic into memory.</summary>
    /// <param name="password">Vault password. Required on Web/Extension/CLI.</param>
    /// <param name="biometricReason">Prompt reason for biometric auth on MAUI.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task UnlockAsync(ReadOnlyMemory<byte>? password = null, string? biometricReason = null, CancellationToken ct = default)
    {
        if (IsUnlocked) return;
        _mnemonicBytes = await EnsureStorage().UnlockVaultAsync(Id, password, biometricReason, ct);
    }

    /// <summary>Zeros the in-memory mnemonic and locks the wallet.</summary>
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

    /// <summary>Returns the account at the current active index, or null if not found.</summary>
    public BurizaWalletAccount? GetActiveAccount() =>
        Accounts.FirstOrDefault(a => a.Index == ActiveAccountIndex);

    /// <summary>Derives chain address data for the given account. Wallet must be unlocked.</summary>
    /// <param name="accountIndex">Account index. Defaults to the active account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Address data, or null if the account is not found.</returns>
    public async Task<ChainAddressData?> GetAddressInfoAsync(int? accountIndex = null, CancellationToken ct = default)
    {
        if (!IsUnlocked)
            throw new InvalidOperationException("Wallet must be unlocked.");

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
    internal IChainWallet ChainWallet
    {
        get
        {
            ChainInfo chainInfo = ChainRegistry.Get(ActiveChain, Network);
            return EnsureFactory().CreateChainWallet(chainInfo);
        }
    }

    #endregion

    #region Query Operations

    /// <summary>Gets the ADA balance for an account. Wallet must be unlocked.</summary>
    /// <param name="accountIndex">Account index. Defaults to the active account.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<ulong> GetBalanceAsync(int? accountIndex = null, CancellationToken ct = default)
    {
        string? address = (await GetAddressInfoAsync(accountIndex, ct))?.Address;
        if (string.IsNullOrEmpty(address)) return 0;

        using IBurizaChainProvider provider = EnsureProvider();
        return await provider.GetBalanceAsync(address, ct);
    }

    /// <summary>Gets native assets for an account. Wallet must be unlocked.</summary>
    /// <param name="accountIndex">Account index. Defaults to the active account.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyList<Asset>> GetAssetsAsync(int? accountIndex = null, CancellationToken ct = default)
    {
        string? address = (await GetAddressInfoAsync(accountIndex, ct))?.Address;
        if (string.IsNullOrEmpty(address)) return [];

        using IBurizaChainProvider provider = EnsureProvider();
        return await provider.GetAssetsAsync(address, ct);
    }

    /// <summary>Gets UTxOs for an account. Wallet must be unlocked.</summary>
    /// <param name="accountIndex">Account index. Defaults to the active account.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyList<Utxo>> GetUtxosAsync(int? accountIndex = null, CancellationToken ct = default)
    {
        string? address = (await GetAddressInfoAsync(accountIndex, ct))?.Address;
        if (string.IsNullOrEmpty(address)) return [];

        using IBurizaChainProvider provider = EnsureProvider();
        return await provider.GetUtxosAsync(address, ct);
    }

    #endregion

    #region Account Management

    private const int MaxAccountDiscoveryLimit = 50;
    private const string DefaultAccountName = "Account {0}";

    /// <summary>Creates a new account in this wallet and persists it.</summary>
    /// <param name="name">Display name for the account.</param>
    /// <param name="accountIndex">Specific index, or auto-increments from the highest existing index.</param>
    /// <param name="ct">Cancellation token.</param>
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

    /// <summary>Sets the active account index. Account must exist.</summary>
    /// <param name="accountIndex">The account index to activate.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SetActiveAccountAsync(int accountIndex, CancellationToken ct = default)
    {
        _ = Accounts.FirstOrDefault(a => a.Index == accountIndex)
            ?? throw new ArgumentException($"Account {accountIndex} not found", nameof(accountIndex));

        ActiveAccountIndex = accountIndex;
        LastAccessedAt = DateTime.UtcNow;
        await SaveAsync(ct);
    }

    /// <summary>Updates account display properties. Account must exist.</summary>
    /// <param name="accountIndex">The account to update.</param>
    /// <param name="name">New display name, or null to keep current.</param>
    /// <param name="avatar">New avatar, or null to keep current.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task UpdateAccountAsync(int accountIndex, string? name = null, string? avatar = null, CancellationToken ct = default)
    {
        BurizaWalletAccount account = Accounts.FirstOrDefault(a => a.Index == accountIndex)
            ?? throw new ArgumentException($"Account {accountIndex} not found", nameof(accountIndex));

        if (name is not null) account.Name = name;
        if (avatar is not null) account.Avatar = avatar;

        await SaveAsync(ct);
    }

    /// <summary>Scans the chain for used accounts up to a gap limit. Wallet must be unlocked.</summary>
    /// <param name="accountGapLimit">Number of consecutive empty accounts before stopping.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Newly discovered accounts.</returns>
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

    /// <summary>Switches the wallet to a different chain/network.</summary>
    /// <param name="chainInfo">Target chain and network.</param>
    /// <param name="ct">Cancellation token.</param>
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

    /// <summary>Verifies a password against the wallet's vault.</summary>
    /// <param name="password">Password to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<bool> VerifyPasswordAsync(ReadOnlyMemory<byte> password, CancellationToken ct = default)
        => EnsureStorage().VerifyPasswordAsync(Id, password, ct);

    /// <summary>Changes the wallet's vault password.</summary>
    /// <param name="currentPassword">Current password for decryption.</param>
    /// <param name="newPassword">New password for re-encryption.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task ChangePasswordAsync(ReadOnlyMemory<byte> currentPassword, ReadOnlyMemory<byte> newPassword, CancellationToken ct = default)
        => EnsureStorage().ChangePasswordAsync(Id, currentPassword, newPassword, ct);

    #endregion

    #region Sensitive Operations

    /// <summary>Returns the mnemonic phrase as a UTF-8 string. Wallet must be unlocked.</summary>
    public string ExportMnemonic()
    {
        if (!IsUnlocked)
            throw new InvalidOperationException("Wallet must be unlocked to export mnemonic.");

        return Encoding.UTF8.GetString(_mnemonicBytes!);
    }

    #endregion

    #region Custom Provider Config

    /// <summary>Gets the custom provider config for a chain, or null if not set.</summary>
    public Task<CustomProviderConfig?> GetCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
        => EnsureStorage().GetCustomProviderConfigAsync(chainInfo, ct);

    /// <summary>Sets a custom provider endpoint and optional API key for a chain.</summary>
    public async Task SetCustomProviderConfigAsync(ChainInfo chainInfo, string? endpoint, string? apiKey, ReadOnlyMemory<byte>? password = null, string? name = null, CancellationToken ct = default)
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

    /// <summary>Removes the custom provider config for a chain.</summary>
    public async Task ClearCustomProviderConfigAsync(ChainInfo chainInfo, CancellationToken ct = default)
    {
        await EnsureStorage().DeleteCustomProviderConfigAsync(chainInfo, ct);
        EnsureFactory().ClearChainConfig(chainInfo);
    }

    /// <summary>Loads stored custom provider config into the runtime factory.</summary>
    public async Task LoadCustomProviderConfigAsync(ChainInfo chainInfo, ReadOnlyMemory<byte>? password = null, CancellationToken ct = default)
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

    internal Task SaveAsync(CancellationToken ct = default)
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
        byte[] signedTxBytes = chainWallet.Sign(unsignedTx, _mnemonicBytes, chainInfo, ActiveAccountIndex, 0);
        return await provider.SubmitAsync(signedTxBytes, ct);
    }

    #endregion

    /// <summary>Locks the wallet and releases resources.</summary>
    public void Dispose()
    {
        Lock();
        GC.SuppressFinalize(this);
    }

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
