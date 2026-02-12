using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Pages;

public partial class Settings
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    [Inject]
    public required NavigationManager NavigationManager { get; set; }

    private void HandleAccountSettingsClick()
    {
        NavigationManager.NavigateTo("/settings/account");
    }

    private void HandleNodeClick()
    {
        NavigationManager.NavigateTo("/settings/node");
    }

    private void HandleSeeAllWallets()
    {
        NavigationManager.NavigateTo("/settings/wallets");
    }

    private void HandleSwitchAccount(string walletName)
    {
        AppStateService.SelectedWalletName = walletName;
        NavigationManager.NavigateTo("/settings/wallet");
    }
}
