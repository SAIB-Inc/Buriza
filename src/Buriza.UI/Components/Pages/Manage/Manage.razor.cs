namespace Buriza.UI.Components.Pages;

public partial class Manage
{
    protected bool IsAccountFormVisible { get; set; }
    protected bool IsEditMode { get; set; }
    protected bool[] AccountStates { get; set; } = new bool[9];
    protected bool[] WalletExpandedStates { get; set; } = new bool[3];
    
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
    }
}