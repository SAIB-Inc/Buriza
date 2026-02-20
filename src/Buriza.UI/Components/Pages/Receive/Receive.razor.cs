using Buriza.UI.Services;
using Buriza.UI.Data.Dummy;
using Buriza.Data.Models.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace Buriza.UI.Components.Pages;

public partial class Receive : ComponentBase
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    [Inject]
    public required IJSRuntime JSRuntime { get; set; }

    protected MudCarousel<object>? _carousel;
    private Wallet? _expandedWallet;
    protected Wallet? CurrentWallet { get; set; }
    protected WalletAccount? CurrentWalletAccount { get; set; }
    protected List<Wallet> Wallets { get; set; } = [];

    protected override void OnInitialized()
    {
        Wallets = WalletData.GetDummyWallets();
        foreach (var wallet in Wallets)
        {
            WalletAccount? account = wallet.Accounts.FirstOrDefault(a => a.IsActive);
            wallet.IsExpanded = account != null;
            if (account != null)
            {
                CurrentWallet = wallet;
                CurrentWalletAccount = account;
                _expandedWallet = wallet;
                break;
            }
        }
    }

    protected void OnAccountCardClicked(Wallet wallet, WalletAccount account)
    {
        if (CurrentWalletAccount != null)
            CurrentWalletAccount.IsActive = false;

        if (_expandedWallet != null)
            _expandedWallet.IsExpanded = false;
        wallet.IsExpanded = true;
        CurrentWallet = wallet;
        _expandedWallet = wallet;

        account.IsActive = true;
        CurrentWalletAccount = account;
        _carousel?.Previous();
    }

    protected void ToggleWalletExpansion(Wallet wallet)
    {
        if (_expandedWallet != null)
            _expandedWallet.IsExpanded = false;

        if (_expandedWallet == wallet)
        {
            _expandedWallet = null;
        }
        else
        {
            wallet.IsExpanded = true;
            _expandedWallet = wallet;
        }
    }

    protected void OnAdvancedModeButtonClicked()
    {
        _carousel?.Next();
    }

    protected void OnBackButtonClicked()
    {
        _carousel?.Previous();
    }

    protected async Task CopyAddressToClipboard(string address)
    {
        await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", address);
    }
}
