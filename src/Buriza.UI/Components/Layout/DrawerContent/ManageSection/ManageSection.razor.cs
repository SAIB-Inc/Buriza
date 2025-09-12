using Buriza.UI.Resources;
using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Layout;

public partial class ManageSection : ComponentBase, IDisposable
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected bool IsAccountFormVisible { get; set; } = false;
    protected bool IsEditMode { get; set; } = false;    
    public static bool IsManageAccountFormVisible { get; private set; } = false;
    public static bool IsManageEditMode { get; private set; } = false;
    public static Action? OnManageStateChanged { get; set; }
    
    protected bool[] WalletExpandedStates { get; set; } = new bool[6];
    
    protected bool[] AccountStates { get; set; } = new bool[18];

    protected void ToggleWallet(int walletIndex)
    {
        WalletExpandedStates[walletIndex] = !WalletExpandedStates[walletIndex];
    }

    protected void ShowEditWallet()
    {
        IsEditMode = true;
        IsAccountFormVisible = true;
        UpdateStaticState();
    }

    protected void ShowAddAccount()
    {
        IsEditMode = false;
        IsAccountFormVisible = true;
        UpdateStaticState();
    }

    public static void HideAccountForm()
    {
        IsManageAccountFormVisible = false;
        IsManageEditMode = false;
        OnManageStateChanged?.Invoke();
    }
    
    protected void HideAccountFormInstance()
    {
        IsAccountFormVisible = false;
        IsEditMode = false;
        UpdateStaticState();
    }
    
    private void UpdateStaticState()
    {
        IsManageAccountFormVisible = IsAccountFormVisible;
        IsManageEditMode = IsEditMode;
        OnManageStateChanged?.Invoke();
    }
    
    protected void HandleAddWalletClick()
    {
        AppStateService.IsFilterDrawerOpen = false;
    }
    
    private void SyncFromStaticState()
    {
        IsAccountFormVisible = IsManageAccountFormVisible;
        IsEditMode = IsManageEditMode;
        StateHasChanged();
    }
    
    protected override void OnInitialized()
    {
        OnManageStateChanged += SyncFromStaticState;
        
        for (int i = 0; i < 6; i++)
        {
            AccountStates[i * 3] = true;
        }
    }
    
    public void Dispose()
    {
        OnManageStateChanged -= SyncFromStaticState;
    }
}