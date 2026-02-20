using System.Collections.Concurrent;
using Buriza.Core.Interfaces;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Buriza.Data.Services;
using Buriza.Core.Interfaces.DataServices;

namespace Buriza.UI.Services;

public class AppStateService : IBurizaAppStateService, IDataServiceSessionProvider, IDisposable
{
    private readonly ConcurrentDictionary<string, string> _cache = new();
    private bool _disposed;

    #region Wallet Session State

    private bool _isInitialized;
    public bool IsInitialized
    {
        get => _isInitialized;
        private set => SetProperty(ref _isInitialized, value);
    }

    private bool _hasWallets;
    public bool HasWallets
    {
        get => _hasWallets;
        private set => SetProperty(ref _hasWallets, value);
    }

    private bool _isUnlocked;
    public bool IsUnlocked
    {
        get => _isUnlocked;
        private set => SetProperty(ref _isUnlocked, value);
    }

    private WalletInfo? _activeWallet;
    public WalletInfo? ActiveWallet
    {
        get => _activeWallet;
        private set => SetProperty(ref _activeWallet, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    private ulong _currentSlot;
    public ulong CurrentSlot
    {
        get => _currentSlot;
        set => SetProperty(ref _currentSlot, value);
    }

    #endregion

    #region Cached Balance & Price (updated on heartbeat)

    private ulong _balanceLovelace;
    public ulong BalanceLovelace
    {
        get => _balanceLovelace;
        set => SetProperty(ref _balanceLovelace, value);
    }

    private decimal _adaPriceUsd;
    public decimal AdaPriceUsd
    {
        get => _adaPriceUsd;
        set => SetProperty(ref _adaPriceUsd, value);
    }

    private decimal _priceChange24h;
    public decimal PriceChange24h
    {
        get => _priceChange24h;
        set => SetProperty(ref _priceChange24h, value);
    }

    public decimal BalanceAda => _balanceLovelace / 1_000_000m;
    public decimal BalanceUsd => BalanceAda * _adaPriceUsd;

    #endregion

    #region UI State

    private bool _isDarkMode = true;
    public bool IsDarkMode
    {
        get => _isDarkMode;
        set => SetProperty(ref _isDarkMode, value);
    }

    private AssetType _selectedAssetType = AssetType.Token;
    public AssetType SelectedAssetType
    {
        get => _selectedAssetType;
        set => SetProperty(ref _selectedAssetType, value);
    }

    public int SelectedAssetTypeIndex
    {
        get => (int)_selectedAssetType;
        set
        {
            if ((int)_selectedAssetType != value)
            {
                _selectedAssetType = (AssetType)value;
                NotifyChanged();
            }
        }
    }

    private bool _isSidebarOpen;
    public bool IsSidebarOpen
    {
        get => _isSidebarOpen;
        set => SetProperty(ref _isSidebarOpen, value);
    }

    private bool _isFilterDrawerOpen;
    public bool IsFilterDrawerOpen
    {
        get => _isFilterDrawerOpen;
        set => SetProperty(ref _isFilterDrawerOpen, value);
    }

    private DrawerContentType _currentDrawerContent = DrawerContentType.None;
    public DrawerContentType CurrentDrawerContent
    {
        get => _currentDrawerContent;
        set => SetProperty(ref _currentDrawerContent, value);
    }

    public string SelectedWalletName { get; set; } = string.Empty;

    private TransactionType? _summaryTransactionType;
    public TransactionType? SummaryTransactionType
    {
        get => _summaryTransactionType;
        set
        {
            if (_summaryTransactionType != value)
            {
                _summaryTransactionType = value;
                NotifyChanged();
            }
        }
    }

    private TransactionCategory _summaryTransactionCategory = TransactionCategory.Default;
    public TransactionCategory SummaryTransactionCategory
    {
        get => _summaryTransactionCategory;
        set
        {
            if (_summaryTransactionCategory != value)
            {
                _summaryTransactionCategory = value;
                NotifyChanged();
            }
        }
    }

    private SidebarContentType _currentSidebarContent = SidebarContentType.None;
    public SidebarContentType CurrentSidebarContent
    {
        get => _currentSidebarContent;
        set => SetProperty(ref _currentSidebarContent, value);
    }

    private bool _isSendConfirmed;
    public bool IsSendConfirmed
    {
        get => _isSendConfirmed;
        set => SetProperty(ref _isSendConfirmed, value);
    }

    private bool _isReceiveAdvancedMode;
    public bool IsReceiveAdvancedMode
    {
        get => _isReceiveAdvancedMode;
        set => SetProperty(ref _isReceiveAdvancedMode, value);
    }

    private bool _isManageAccountFormVisible;
    public bool IsManageAccountFormVisible
    {
        get => _isManageAccountFormVisible;
        set => SetProperty(ref _isManageAccountFormVisible, value);
    }

    private bool _isManageEditMode;
    public bool IsManageEditMode
    {
        get => _isManageEditMode;
        set => SetProperty(ref _isManageEditMode, value);
    }

    #endregion

    #region Wallet Session Methods

    public void SetInitialized(bool hasWallets)
    {
        HasWallets = hasWallets;
        IsInitialized = true;
    }

    public void SetUnlocked(WalletInfo wallet)
    {
        ActiveWallet = wallet;
        IsUnlocked = true;
    }

    public void Lock()
    {
        IsUnlocked = false;
        ActiveWallet = null;
        BalanceLovelace = 0;
        AdaPriceUsd = 0;
        PriceChange24h = 0;
    }

    public void UpdateActiveWallet(WalletInfo? wallet)
    {
        ActiveWallet = wallet;
    }

    #endregion

    #region UI Methods

    public void SetDrawerContent(DrawerContentType contentType)
    {
        CurrentDrawerContent = contentType;
        IsFilterDrawerOpen = true;
    }

    public void RequestAddRecipient() => OnAddRecipientRequested?.Invoke();

    public void RequestResetSendConfirmation() => OnResetSendConfirmationRequested?.Invoke();

    public void HideManageAccountForm()
    {
        IsManageAccountFormVisible = false;
        IsManageEditMode = false;
    }

    #endregion

    #region Events

    public event Action? OnChanged;
    public event Action? OnAddRecipientRequested;
    public event Action? OnResetSendConfirmationRequested;

    private void NotifyChanged() => OnChanged?.Invoke();

    private void SetProperty<T>(ref T field, T value)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            NotifyChanged();
        }
    }

    #endregion

    #region Address Cache

    public string? GetCachedAddress(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange)
        => CacheGet($"address:{walletId}:{GetChainKey(chainInfo)}:{accountIndex}:{(isChange ? 1 : 0)}:{addressIndex}");

    public void CacheAddress(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange, string address)
        => _cache.TryAdd($"address:{walletId}:{GetChainKey(chainInfo)}:{accountIndex}:{(isChange ? 1 : 0)}:{addressIndex}", address);

    public bool HasCachedAddresses(Guid walletId, ChainInfo chainInfo, int accountIndex)
        => _cache.Keys.Any(k => k.StartsWith($"address:{walletId}:{GetChainKey(chainInfo)}:{accountIndex}:"));

    public void ClearWalletCache(Guid walletId)
        => CacheRemoveByPrefix($"address:{walletId}:");

    #endregion

    #region Custom Chain Config (IBurizaAppStateService)

    public ServiceConfig? GetChainConfig(ChainInfo chainInfo)
    {
        string key = $"chain:{GetChainKey(chainInfo)}";
        string? endpoint = CacheGet($"{key}:endpoint");
        string? apiKey = CacheGet($"{key}:apiKey");
        return endpoint is null && apiKey is null ? null : new ServiceConfig(endpoint, apiKey);
    }

    public void SetChainConfig(ChainInfo chainInfo, ServiceConfig config)
    {
        string key = $"chain:{GetChainKey(chainInfo)}";
        if (config.Endpoint is not null)
            _cache[$"{key}:endpoint"] = config.Endpoint;
        else
            _cache.TryRemove($"{key}:endpoint", out _);

        if (config.ApiKey is not null)
            _cache[$"{key}:apiKey"] = config.ApiKey;
        else
            _cache.TryRemove($"{key}:apiKey", out _);
    }

    public void ClearChainConfig(ChainInfo chainInfo)
    {
        string key = $"chain:{GetChainKey(chainInfo)}";
        _cache.TryRemove($"{key}:endpoint", out _);
        _cache.TryRemove($"{key}:apiKey", out _);
    }

    #endregion

    #region Data Service Config (IDataServiceSessionProvider)

    public ServiceConfig? GetDataServiceConfig(DataServiceType serviceType)
    {
        string key = $"data:{(int)serviceType}";
        string? endpoint = CacheGet($"{key}:endpoint");
        string? apiKey = CacheGet($"{key}:apiKey");
        return endpoint is null && apiKey is null ? null : new ServiceConfig(endpoint, apiKey);
    }

    public void SetDataServiceConfig(DataServiceType serviceType, ServiceConfig config)
    {
        string key = $"data:{(int)serviceType}";
        if (config.Endpoint is not null)
            _cache[$"{key}:endpoint"] = config.Endpoint;
        else
            _cache.TryRemove($"{key}:endpoint", out _);

        if (config.ApiKey is not null)
            _cache[$"{key}:apiKey"] = config.ApiKey;
        else
            _cache.TryRemove($"{key}:apiKey", out _);
    }

    public void ClearDataServiceConfig(DataServiceType serviceType)
    {
        string key = $"data:{(int)serviceType}";
        _cache.TryRemove($"{key}:endpoint", out _);
        _cache.TryRemove($"{key}:apiKey", out _);
    }

    // IDataServiceSessionProvider implementation
    public string? GetDataServiceEndpoint(DataServiceType serviceType)
        => GetDataServiceConfig(serviceType)?.Endpoint;

    public string? GetDataServiceApiKey(DataServiceType serviceType)
        => GetDataServiceConfig(serviceType)?.ApiKey;

    #endregion

    #region Cache Helpers

    private static string GetChainKey(ChainInfo chainInfo) => $"{(int)chainInfo.Chain}:{chainInfo.Network}";

    private string? CacheGet(string key)
        => _cache.TryGetValue(key, out string? value) ? value : null;

    private void CacheRemoveByPrefix(string prefix)
    {
        foreach (string key in _cache.Keys.Where(k => k.StartsWith(prefix)).ToList())
            _cache.TryRemove(key, out _);
    }

    public void ClearCache() => _cache.Clear();

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cache.Clear();
        GC.SuppressFinalize(this);
    }

    #endregion
}
