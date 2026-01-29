using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Asset = Buriza.Data.Models.Common.Asset;

namespace Buriza.Core.Models;

/// <summary>
/// HD Wallet following BIP-39/BIP-44 standards.
/// One wallet = one mnemonic seed that can derive keys for multiple chains.
/// </summary>
public class BurizaWallet : IWallet
{
    public required int Id { get; init; }
    public required string Name { get; set; }
    public string Avatar { get; set; } = string.Empty;

    /// <summary>Current active chain for this wallet.</summary>
    public ChainType ActiveChain { get; set; } = ChainType.Cardano;

    /// <summary>Current active account index.</summary>
    public int ActiveAccountIndex { get; set; } = 0;

    /// <summary>All accounts in this wallet.</summary>
    public List<WalletAccount> Accounts { get; set; } = [];

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>Per-chain provider configurations (endpoint, network, apiKey).</summary>
    public Dictionary<ChainType, ProviderConfig> ProviderConfigs { get; set; } = [];

    /// <summary>Chain provider for querying balance, assets, etc.</summary>
    internal IChainProvider? Provider { get; set; }

    #region Account & Address Helpers

    public WalletAccount? GetActiveAccount() =>
        Accounts.FirstOrDefault(a => a.Index == ActiveAccountIndex);

    public string? GetPrimaryAddress()
    {
        WalletAccount? account = GetActiveAccount();
        ChainAddressData? chainData = account?.GetChainData(ActiveChain);
        return chainData?.ExternalAddresses.FirstOrDefault()?.Address;
    }

    public IReadOnlyList<string> GetAddresses(int? accountIndex = null, bool includeChange = false)
    {
        WalletAccount? account = accountIndex.HasValue
            ? Accounts.FirstOrDefault(a => a.Index == accountIndex.Value)
            : GetActiveAccount();

        ChainAddressData? chainData = account?.GetChainData(ActiveChain);
        if (chainData == null) return [];

        List<string> addresses = [.. chainData.ExternalAddresses.Select(a => a.Address)];
        if (includeChange)
            addresses.AddRange(chainData.InternalAddresses.Select(a => a.Address));

        return addresses;
    }

    #endregion

    #region IWallet - Query Operations

    public async Task<ulong> GetBalanceAsync(int? accountIndex = null, bool allAddresses = false, CancellationToken ct = default)
    {
        EnsureProvider();

        IReadOnlyList<string> addresses = GetAddresses(accountIndex, allAddresses);
        if (addresses.Count == 0) return 0;

        if (!allAddresses)
            return await Provider!.QueryService.GetBalanceAsync(addresses[0], ct);

        IEnumerable<Task<ulong>> tasks = addresses.Select(a => Provider!.QueryService.GetBalanceAsync(a, ct));
        ulong[] balances = await Task.WhenAll(tasks);
        return balances.Aggregate(0UL, (total, b) => total + b);
    }

    public async Task<IReadOnlyList<Asset>> GetAssetsAsync(int? accountIndex = null, bool allAddresses = false, CancellationToken ct = default)
    {
        EnsureProvider();

        IReadOnlyList<string> addresses = GetAddresses(accountIndex, allAddresses);
        if (addresses.Count == 0) return [];

        if (!allAddresses)
            return await Provider!.QueryService.GetAssetsAsync(addresses[0], ct);

        IEnumerable<Task<IReadOnlyList<Asset>>> tasks = addresses.Select(a => Provider!.QueryService.GetAssetsAsync(a, ct));
        IReadOnlyList<Asset>[] results = await Task.WhenAll(tasks);

        return [.. results
            .SelectMany(assets => assets)
            .GroupBy(a => a.Subject)
            .Select(g => g.First() with { Quantity = g.Aggregate(0UL, (sum, a) => sum + a.Quantity) })];
    }

    public async Task<IReadOnlyList<Utxo>> GetUtxosAsync(int? accountIndex = null, CancellationToken ct = default)
    {
        EnsureProvider();

        IReadOnlyList<string> addresses = GetAddresses(accountIndex, includeChange: true);
        if (addresses.Count == 0) return [];

        IEnumerable<Task<IReadOnlyList<Utxo>>> tasks = addresses.Select(a => Provider!.QueryService.GetUtxosAsync(a, ct));
        IReadOnlyList<Utxo>[] results = await Task.WhenAll(tasks);

        return [.. results.SelectMany(u => u)];
    }

    public async Task<IReadOnlyList<TransactionHistory>> GetTransactionHistoryAsync(int? accountIndex = null, int limit = 50, CancellationToken ct = default)
    {
        EnsureProvider();

        string? address = GetAddresses(accountIndex).FirstOrDefault();
        if (address == null) return [];

        return await Provider!.QueryService.GetTransactionHistoryAsync(address, limit, ct);
    }

    #endregion

    #region IWallet - Transaction Operations

    public async Task<UnsignedTransaction> BuildTransactionAsync(ulong amount, string toAddress, CancellationToken ct = default)
    {
        EnsureProvider();

        string? fromAddress = GetPrimaryAddress()
            ?? throw new InvalidOperationException("No address available");

        TransactionRequest request = new()
        {
            FromAddress = fromAddress,
            Recipients = [new TransactionRecipient { Address = toAddress, Amount = amount }]
        };

        return await Provider!.TransactionService.BuildAsync(request, ct);
    }

    public async Task<UnsignedTransaction> BuildTransactionAsync(TransactionRequest request, CancellationToken ct = default)
    {
        EnsureProvider();

        // Use wallet's primary address if FromAddress not specified
        if (string.IsNullOrEmpty(request.FromAddress))
        {
            request = request with
            {
                FromAddress = GetPrimaryAddress()
                    ?? throw new InvalidOperationException("No address available")
            };
        }

        return await Provider!.TransactionService.BuildAsync(request, ct);
    }

    public async Task<string> SubmitAsync(Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction tx, CancellationToken ct = default)
    {
        EnsureProvider();
        return await Provider!.TransactionService.SubmitAsync(tx, ct);
    }

    #endregion

    private void EnsureProvider()
    {
        if (Provider == null)
            throw new InvalidOperationException("Wallet is not connected to a provider. Use WalletManager to load the wallet.");
    }
}

/// <summary>
/// Account within an HD wallet (BIP-44 account level).
/// m / purpose' / coin_type' / account' / ...
/// </summary>
public class WalletAccount
{
    /// <summary>BIP-44 account index (hardened).</summary>
    public required int Index { get; init; }

    public required string Name { get; set; }
    public string Avatar { get; set; } = string.Empty;

    /// <summary>Chain-specific address data, keyed by ChainType.</summary>
    public Dictionary<ChainType, ChainAddressData> ChainData { get; set; } = [];

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public ChainAddressData? GetChainData(ChainType chain) =>
        ChainData.TryGetValue(chain, out ChainAddressData? data) ? data : null;
}

/// <summary>
/// Chain-specific address data for an account.
/// </summary>
public class ChainAddressData
{
    public required ChainType Chain { get; init; }

    /// <summary>External (receive) addresses - role 0.</summary>
    public List<AddressInfo> ExternalAddresses { get; set; } = [];

    /// <summary>Internal (change) addresses - role 1.</summary>
    public List<AddressInfo> InternalAddresses { get; set; } = [];

    /// <summary>Next unused external address index.</summary>
    public int NextExternalIndex { get; set; } = 0;

    /// <summary>Next unused internal address index.</summary>
    public int NextInternalIndex { get; set; } = 0;

    /// <summary>Last time this chain data was synced with the network.</summary>
    public DateTime? LastSyncedAt { get; set; }
}

/// <summary>
/// Individual address information.
/// </summary>
public class AddressInfo
{
    /// <summary>Address index in derivation path.</summary>
    public required int Index { get; init; }

    /// <summary>Bech32 or other format address string.</summary>
    public required string Address { get; init; }

    /// <summary>Whether this address has been used on-chain.</summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>Optional label for this address.</summary>
    public string? Label { get; set; }
}

/// <summary>
/// Derivation path constants for supported chains.
/// </summary>
public static class DerivationPaths
{
    /// <summary>
    /// Cardano CIP-1852: m/1852'/1815'/account'/role/index
    /// </summary>
    public static class Cardano
    {
        public const int Purpose = 1852;
        public const int CoinType = 1815;

        public static string GetBasePath(int account) =>
            $"m/{Purpose}'/{CoinType}'/{account}'";

        public static string GetFullPath(int account, int role, int index) =>
            $"m/{Purpose}'/{CoinType}'/{account}'/{role}/{index}";
    }

    /// <summary>
    /// Bitcoin BIP-84 (Native SegWit): m/84'/0'/account'/change/index
    /// </summary>
    public static class Bitcoin
    {
        public const int Purpose = 84;
        public const int CoinType = 0;

        public static string GetBasePath(int account) =>
            $"m/{Purpose}'/{CoinType}'/{account}'";

        public static string GetFullPath(int account, int change, int index) =>
            $"m/{Purpose}'/{CoinType}'/{account}'/{change}/{index}";
    }

    /// <summary>
    /// Ethereum BIP-44: m/44'/60'/account'/0/index
    /// </summary>
    public static class Ethereum
    {
        public const int Purpose = 44;
        public const int CoinType = 60;

        public static string GetBasePath(int account) =>
            $"m/{Purpose}'/{CoinType}'/{account}'";

        public static string GetFullPath(int account, int index) =>
            $"m/{Purpose}'/{CoinType}'/{account}'/0/{index}";
    }
}
