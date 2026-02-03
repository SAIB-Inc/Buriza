using Buriza.Data.Models.Common;

namespace Buriza.Data.Services;

/// <summary>
/// UI facade service that provides wallet data in display-ready formats.
/// Bridges Core wallet operations with Data services (prices, metadata).
/// </summary>
public interface IWalletDataService
{
    /// <summary>Fired when wallet data changes (balance, assets, etc.).</summary>
    event Action? OnDataChanged;

    #region Wallet Info (rarely changes)

    /// <summary>Gets basic wallet info (name, account, address). Does not query chain.</summary>
    Task<WalletInfo?> GetWalletInfoAsync(CancellationToken ct = default);

    #endregion

    #region Balance & Prices (refresh periodically)

    /// <summary>Gets current ADA balance in lovelace.</summary>
    Task<ulong> GetBalanceAsync(CancellationToken ct = default);

    /// <summary>Gets ADA price in the specified currency.</summary>
    Task<decimal?> GetAdaPriceAsync(string quote = "USD", CancellationToken ct = default);

    /// <summary>Gets ADA price change percentage for the specified timeframe.</summary>
    Task<decimal> GetAdaPriceChangeAsync(string timeframe = "24h", CancellationToken ct = default);

    #endregion

    #region Assets (refresh on heartbeat)

    /// <summary>Gets token assets with prices and metadata.</summary>
    Task<IReadOnlyList<TokenAsset>> GetTokenAssetsAsync(bool refresh = false, CancellationToken ct = default);

    /// <summary>Gets NFT assets.</summary>
    Task<IReadOnlyList<NftAsset>> GetNftAssetsAsync(bool refresh = false, CancellationToken ct = default);

    #endregion

    #region Transactions & Addresses

    /// <summary>Gets transaction history for the active account.</summary>
    Task<IReadOnlyList<TransactionHistory>> GetTransactionHistoryAsync(int limit = 50, CancellationToken ct = default);

    /// <summary>Gets the receive address for the active account.</summary>
    Task<ReceiveAccount?> GetReceiveAddressAsync(CancellationToken ct = default);

    #endregion

    /// <summary>Clears cached data and triggers OnDataChanged.</summary>
    Task RefreshAsync(CancellationToken ct = default);
}

/// <summary>
/// Basic wallet information (does not require chain queries).
/// </summary>
public record WalletInfo
{
    public required int WalletId { get; init; }
    public required string WalletName { get; init; }
    public required string AccountName { get; init; }
    public required int AccountIndex { get; init; }
    public required string PrimaryAddress { get; init; }
}
