using Buriza.UI.Resources;
using Buriza.UI.Services;
using Buriza.UI.Data;
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
    protected int _selectedAccountIndex = 0;
    protected ReceiveAccount SelectedAccount => Accounts[_selectedAccountIndex];
    protected List<ReceiveAccount> Accounts { get; set; } = new();

    protected override void OnInitialized()
    {
        Accounts = ReceiveAccountData.GetDummyReceiveAccounts();
    }

    protected void OnAccountCardClicked(int index)
    {
        _selectedAccountIndex = index;
        _carousel?.Previous();
    }

    protected void OnAdvancedModeButtonClicked()
    {
        _carousel?.Next();
    }

    protected void OnBackButtonClicked()
    {
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
