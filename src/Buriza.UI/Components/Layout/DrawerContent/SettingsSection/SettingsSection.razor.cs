using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using static Buriza.Data.Models.Enums.DrawerContentType;

namespace Buriza.UI.Components.Layout;

public partial class SettingsSection : ComponentBase
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected bool IsDarkMode { get; set; } = true;

    protected void HandleAccountSettingsClick()
    {
        AppStateService.SetDrawerContent(AccountSettings);
    }

    protected void HandleNodeClick()
    {
        AppStateService.SetDrawerContent(NodeSettings);
    }

    protected void HandleSeeAllWallets()
    {
        AppStateService.SetDrawerContent(SwitchWallet);
    }

    protected void HandleSwitchAccount(string walletName)
    {
        AppStateService.SelectedWalletName = walletName;
        AppStateService.SetDrawerContent(SwitchAccount);
    }
}