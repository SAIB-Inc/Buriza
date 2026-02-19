using Buriza.UI.Services;
using Buriza.UI.Data.Dummy;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Buriza.UI.Components.Layout;

public partial class ReceiveSection : ComponentBase, IDisposable
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    [Inject]
    public required IJSRuntime JSRuntime { get; set; }

    protected bool IsAdvancedMode => AppStateService.IsReceiveAdvancedMode;
     protected WalletAccount? SelectedWalletAccount;
    protected List<Wallet> Wallets { get; set; } = [];

    protected override void OnInitialized()
    {
        AppStateService.OnChanged += StateHasChanged;
        Wallets = WalletData.GetDummyWallets();
        SelectedWalletAccount = Wallets                                                                                                                      
            .SelectMany(w => w.Accounts)                                                                                                                     
            .FirstOrDefault(a => a.IsActive);

        foreach (var wallet in Wallets)                                                                                               
        {                                                                                                                             
            wallet.IsExpanded = wallet.Accounts.Any(a => a.IsActive);                                                                 
        }  
    }

    protected void ToggleWallet(Wallet wallet)
    {
        wallet.IsExpanded = !wallet.IsExpanded;
    }

    private async Task CopyAddressToClipboard(string address)
    {
        await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", address);
    }

    protected void OnAccountCardClicked(Wallet wallet, WalletAccount account)
    {
        //TODO: i think selected wallet account should never be null
        if(SelectedWalletAccount != null)
            SelectedWalletAccount.IsActive = false;

        foreach(var w in Wallets)
            w.IsExpanded = w == wallet;

        account.IsActive = true;
        SelectedWalletAccount = account;
        AppStateService.IsReceiveAdvancedMode = false;
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
        AppStateService.OnChanged -= StateHasChanged;
    }
}
