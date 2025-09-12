using Buriza.UI.Data;
using Buriza.Data.Models.Common;

namespace Buriza.UI.Components.Pages;

public partial class Manage
{
    protected bool IsAccountFormVisible { get; set; }
    protected bool IsEditMode { get; set; }
    
    protected List<Wallet> wallets = WalletData.GetDummyWallets();
    protected bool[] WalletExpandedStates = new bool[3];
    
    protected override void OnInitialized()
    {
        wallets = WalletData.GetDummyWallets();
    }
    
    protected void ShowAddAccount()
    {
        IsEditMode = false;
        IsAccountFormVisible = true;
    }
    
    protected void ShowEditWallet()
    {
        IsEditMode = true;
        IsAccountFormVisible = true;
    }
    
    protected void HideAccountForm()
    {
        IsAccountFormVisible = false;
        IsEditMode = false;
    }
    
    protected void ToggleWallet(int walletIndex)
    {
        WalletExpandedStates[walletIndex] = !WalletExpandedStates[walletIndex];
        StateHasChanged();
    }
}