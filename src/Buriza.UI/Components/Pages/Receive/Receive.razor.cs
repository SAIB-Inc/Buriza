using Buriza.UI.Resources;
using Buriza.UI.Services;
using Buriza.UI.Data.Dummy;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Pages;

public partial class Receive : ComponentBase
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected MudCarousel<object>? _carousel;
    protected bool IsAdvancedMode { get; set; }
    protected int _selectedAccountIndex = 0;
    protected ReceiveAccount? SelectedAccount =>
        Accounts.Count > 0 && _selectedAccountIndex < Accounts.Count
            ? Accounts[_selectedAccountIndex]
            : null;
    protected List<ReceiveAccount> Accounts { get; set; } = [];

    protected override void OnInitialized()
    {
        Accounts = ReceiveAccountData.GetDummyReceiveAccounts();
    }

    protected void OnAccountCardClicked(int index)
    {
        _selectedAccountIndex = index;
        IsAdvancedMode = false;
        _carousel?.Previous();
    }

    protected void OnAdvancedModeButtonClicked()
    {
        IsAdvancedMode = true;
        _carousel?.Next();
    }

    protected void OnBackButtonClicked()
    {
        IsAdvancedMode = false;
        _carousel?.Previous();
    }

    protected static string GetChipColor(AccountStatusType status)
    {
        return ReceiveAccountData.GetChipColor(status);
    }

    protected static string GetChipIcon(AccountStatusType status)
    {
        return ReceiveAccountData.GetChipIcon(status);
    }

}
