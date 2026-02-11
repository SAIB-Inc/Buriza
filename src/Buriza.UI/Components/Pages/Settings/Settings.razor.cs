using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Pages;

public partial class Settings
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    [Inject]
    public required NavigationManager NavigationManager { get; set; }

    protected bool IsDarkMode { get; set; } = true;

    protected void HandleAccountSettingsClick()
    {
        NavigationManager.NavigateTo("/settings/account");
    }

    protected void HandleNodeClick()
    {
        NavigationManager.NavigateTo("/settings/node");
    }

    protected void HandleSeeAllWallets()
    {
        NavigationManager.NavigateTo("/settings/wallets");
    }

    protected void HandleSwitchAccount(string walletName)
    {
        AppStateService.SelectedWalletName = walletName;
        NavigationManager.NavigateTo("/settings/wallet");
    }
}
