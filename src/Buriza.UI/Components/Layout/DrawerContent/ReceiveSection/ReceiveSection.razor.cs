using Buriza.UI.Services;
using Buriza.UI.Data;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Layout;

public partial class ReceiveSection : ComponentBase, IDisposable
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected bool IsAdvancedMode => MainLayout.IsReceiveAdvancedMode;
    protected int _selectedAccountIndex = 0;
    protected ReceiveAccount SelectedAccount => Accounts[_selectedAccountIndex];
    protected List<ReceiveAccount> Accounts { get; set; } = [];

    protected override void OnInitialized()
    {
        MainLayout.OnReceiveAdvancedModeChanged += StateHasChanged;
        Accounts = ReceiveAccountData.GetDummyReceiveAccounts();
    }

    protected void OnAccountCardClicked(int accountIndex)
    {
        _selectedAccountIndex = accountIndex;
        MainLayout.SetReceiveAdvancedMode(false);
    }

    protected static string GetChipColor(AccountStatusType status)
    {
        return ReceiveAccountData.GetChipColor(status);
    }

    protected static string GetChipIcon(AccountStatusType status)
    {
        return ReceiveAccountData.GetChipIcon(status);
    }

    public void Dispose()
    {
        MainLayout.OnReceiveAdvancedModeChanged -= StateHasChanged;
    }
}
