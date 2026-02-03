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

    /// <summary>Wallet profile (name, label, avatar).</summary>
    public required WalletProfile Profile { get; set; }

    /// <summary>Network this wallet operates on (Mainnet, Preprod, Preview).</summary>
    public NetworkType Network { get; init; } = NetworkType.Mainnet;

    /// <summary>Current active chain for this wallet.</summary>
    public ChainType ActiveChain { get; set; } = ChainType.Cardano;

    /// <summary>Current active account index.</summary>
    public int ActiveAccountIndex { get; set; } = 0;

    /// <summary>All accounts in this wallet.</summary>
    public List<WalletAccount> Accounts { get; set; } = [];

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>Chain provider for querying balance, assets, etc.</summary>
    internal IChainProvider? Provider { get; set; }

    #region Account & Address Helpers

    public WalletAccount? GetActiveAccount() =>
        Accounts.FirstOrDefault(a => a.Index == ActiveAccountIndex);

    public ChainAddressData? GetAddressInfo(int? accountIndex = null)
    {
        WalletAccount? account = accountIndex.HasValue
            ? Accounts.FirstOrDefault(a => a.Index == accountIndex.Value)
            : GetActiveAccount();

        return account?.GetChainData(ActiveChain);
    }

    #endregion

    #region IWallet - Query Operations

    public async Task<ulong> GetBalanceAsync(int? accountIndex = null, CancellationToken ct = default)
    {
        IChainProvider provider = EnsureProvider();

        string? address = GetAddressInfo(accountIndex)?.ReceiveAddress;
        if (string.IsNullOrEmpty(address)) return 0;

        return await provider.QueryService.GetBalanceAsync(address, ct);
    }

    public async Task<IReadOnlyList<Asset>> GetAssetsAsync(int? accountIndex = null, CancellationToken ct = default)
    {
        IChainProvider provider = EnsureProvider();

        string? address = GetAddressInfo(accountIndex)?.ReceiveAddress;
        if (string.IsNullOrEmpty(address)) return [];

        return await provider.QueryService.GetAssetsAsync(address, ct);
    }

    public async Task<IReadOnlyList<Utxo>> GetUtxosAsync(int? accountIndex = null, CancellationToken ct = default)
    {
        IChainProvider provider = EnsureProvider();

        string? address = GetAddressInfo(accountIndex)?.ReceiveAddress;
        if (string.IsNullOrEmpty(address)) return [];

        return await provider.QueryService.GetUtxosAsync(address, ct);
    }

    public async Task<IReadOnlyList<TransactionHistory>> GetTransactionHistoryAsync(int? accountIndex = null, int limit = 50, CancellationToken ct = default)
    {
        IChainProvider provider = EnsureProvider();

        string? address = GetAddressInfo(accountIndex)?.ReceiveAddress;
        if (string.IsNullOrEmpty(address)) return [];

        return await provider.QueryService.GetTransactionHistoryAsync(address, limit, ct);
    }

    #endregion

    #region IWallet - Transaction Operations

    public async Task<UnsignedTransaction> BuildTransactionAsync(ulong amount, string toAddress, CancellationToken ct = default)
    {
        IChainProvider provider = EnsureProvider();

        string? fromAddress = GetAddressInfo()?.ReceiveAddress;
        if (string.IsNullOrEmpty(fromAddress))
            throw new InvalidOperationException("No address available");

        TransactionRequest request = new()
        {
            FromAddress = fromAddress,
            Recipients = [new TransactionRecipient { Address = toAddress, Amount = amount }]
        };

        return await provider.TransactionService.BuildAsync(request, ct);
    }

    public async Task<UnsignedTransaction> BuildTransactionAsync(TransactionRequest request, CancellationToken ct = default)
    {
        IChainProvider provider = EnsureProvider();

        // Use wallet's primary address if FromAddress not specified
        if (string.IsNullOrEmpty(request.FromAddress))
        {
            string? fromAddress = GetAddressInfo()?.ReceiveAddress;
            if (string.IsNullOrEmpty(fromAddress))
                throw new InvalidOperationException("No address available");

            request = request with { FromAddress = fromAddress };
        }

        return await provider.TransactionService.BuildAsync(request, ct);
    }

    public async Task<string> SubmitAsync(Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction tx, CancellationToken ct = default)
    {
        IChainProvider provider = EnsureProvider();
        return await provider.TransactionService.SubmitAsync(tx, ct);
    }

    #endregion

    private IChainProvider EnsureProvider() =>
        Provider ?? throw new InvalidOperationException("Wallet is not connected to a provider. Use WalletManager to load the wallet.");
}

/// <summary>
/// Account within an HD wallet (BIP-44 account level).
/// m / purpose' / coin_type' / account' / ...
/// </summary>
public class WalletAccount
{
    /// <summary>BIP-44 account index (hardened).</summary>
    public required int Index { get; init; }

    /// <summary>Account name (e.g., "Savings", "Trading").</summary>
    public required string Name { get; set; }

    /// <summary>Chain-specific address data, keyed by ChainType.</summary>
    public Dictionary<ChainType, ChainAddressData> ChainData { get; set; } = [];

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public ChainAddressData? GetChainData(ChainType chain) =>
        ChainData.TryGetValue(chain, out ChainAddressData? data) ? data : null;
}

/// <summary>
/// Chain-specific address data for an account.
/// Stores only the primary receive address. Change addresses derived on-demand during tx build.
/// </summary>
public class ChainAddressData
{
    public required ChainType Chain { get; init; }

    /// <summary>Primary receive address (index 0, role 0).</summary>
    public required string ReceiveAddress { get; set; }

    /// <summary>Staking/reward address (role 2, index 0). Null if not yet derived.</summary>
    public string? StakingAddress { get; set; }

    /// <summary>Last time this chain data was synced with the network.</summary>
    public DateTime? LastSyncedAt { get; set; }
}

/// <summary>
/// Global wallet profile for customization.
/// </summary>
public class WalletProfile
{
    public required string Name { get; set; }
    public string? Label { get; set; }
    public string? Avatar { get; set; }
}

