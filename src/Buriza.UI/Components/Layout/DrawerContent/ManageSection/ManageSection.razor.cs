using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Layout;

public partial class ManageSection : ComponentBase
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected bool IsAccountFormVisible => AppStateService.IsManageAccountFormVisible;
    protected bool IsEditMode => AppStateService.IsManageEditMode;

    protected bool[] WalletExpandedStates { get; set; } = new bool[6];

    protected bool[] AccountStates { get; set; } = new bool[18];

    protected void ToggleWallet(int walletIndex)
    {
        WalletExpandedStates[walletIndex] = !WalletExpandedStates[walletIndex];
    }

    protected void ShowEditWallet()
    {
        AppStateService.IsManageEditMode = true;
        AppStateService.IsManageAccountFormVisible = true;
    }

    protected void ShowAddAccount()
    {
        AppStateService.IsManageEditMode = false;
        AppStateService.IsManageAccountFormVisible = true;
    }

    protected void HandleAddWalletClick()
    {
        AppStateService.IsFilterDrawerOpen = false;
    }

    protected override void OnInitialized()
    {
        for (int i = 0; i < 6; i++)
        {
            AccountStates[i * 3] = true;
        }
    }
}