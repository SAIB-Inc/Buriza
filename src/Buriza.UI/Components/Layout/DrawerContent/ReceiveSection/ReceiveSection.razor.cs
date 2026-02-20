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
    private Wallet? _expandedWallet;
    protected Wallet? CurrentWallet;
    protected WalletAccount? CurrentWalletAccount;
    protected List<Wallet> Wallets { get; set; } = [];

    protected override void OnInitialized()
    {
        AppStateService.OnChanged += StateHasChanged;
        Wallets = WalletData.GetDummyWallets();
        foreach (var wallet in Wallets)                                                                                                                    
        {
            WalletAccount? account = wallet.Accounts.FirstOrDefault(a => a.IsActive);
            wallet.IsExpanded = account != null;
            if (account != null)
            {
                CurrentWallet = wallet;
                CurrentWalletAccount = account;
                _expandedWallet = CurrentWallet;
                break;
            }
        }
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

    private async Task CopyAddressToClipboard(string address)
    {
        await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", address);
    }

    protected void OnAccountCardClicked(Wallet wallet, WalletAccount account)
    {
        //TODO: i think selected wallet account should never be null
        if(CurrentWalletAccount != null)
            CurrentWalletAccount.IsActive = false;

        if (_expandedWallet != null)                                                                                                                         
            _expandedWallet.IsExpanded = false;   
        wallet.IsExpanded = true;
        CurrentWallet = wallet;
        _expandedWallet = wallet;

        account.IsActive = true;
        CurrentWalletAccount = account;
        AppStateService.IsReceiveAdvancedMode = false;
    }

    public void Dispose()
    {
        AppStateService.OnChanged -= StateHasChanged;
    }
}
