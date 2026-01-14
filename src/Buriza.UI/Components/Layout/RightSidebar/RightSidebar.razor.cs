using Buriza.UI.Services;
using Buriza.UI.Data;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using static Buriza.Data.Models.Enums.DrawerContentType;

namespace Buriza.UI.Components.Layout;

public partial class RightSidebar : ComponentBase, IDisposable
{
    [Inject]
    public required AppStateService AppStateService { get; set; }
    
    [Inject] 
    public required NavigationManager Navigation { get; set; }

    protected string SidebarTitle => Navigation.Uri.Contains("/history") ? "Portfolio" : "History";
    protected bool ShowTabs => Navigation.Uri.Contains("/history");
    
    protected List<TokenAsset> TokenAssets { get; set; } = new();
    protected List<NftAsset> NftAssets { get; set; } = new();
    protected Dictionary<string, List<TransactionHistory>> TransactionsByDate { get; set; } = new();

    protected void ToggleTheme()
    {
        AppStateService.IsDarkMode = !AppStateService.IsDarkMode;
    }

    protected void OpenSendDrawer()
    {
        AppStateService.SetDrawerContent(Send);
    }

    protected void OpenReceiveDrawer()
    {
        AppStateService.SetDrawerContent(Receive);
    }

    protected void OpenManageDrawer()
    {
        AppStateService.SetDrawerContent(Manage);
    }

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
        TokenAssets = TokenAssetData.GetDummyTokenAssets();
        NftAssets = NftAssetData.GetDummyNftAssets();
        TransactionsByDate = TransactionHistoryData.GetTransactionsByDate();
    }

    protected void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        StateHasChanged();
    }
    
    protected string GetRelativeTime(DateTime timestamp)
    {
        TimeSpan diff = DateTime.Now - timestamp;

        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} minutes ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} hours ago";
        if (diff.TotalDays < 30)
            return $"{(int)diff.TotalDays} days ago";

        return timestamp.ToString("MMM dd, yyyy");
    }
    
    protected string GetTransactionTypeText(TransactionType type)
    {
        return type == TransactionType.Sent ? "Sent:" : "Received:";
    }
    
    protected string GetTransactionTypeColor(TransactionType type)
    {
        return type == TransactionType.Sent ? "var(--mud-palette-error)" : "var(--mud-palette-success)";
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
    }
}