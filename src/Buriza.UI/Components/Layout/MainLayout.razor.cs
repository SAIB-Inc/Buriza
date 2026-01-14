using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using static Buriza.UI.Services.DrawerContentType;

namespace Buriza.UI.Components.Layout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    protected bool IsHeaderHidden => IsOnRoute(Routes.TransactionSuccess, Routes.Onboard, Routes.Splash);

    private bool IsOnRoute(params string[] routes)
    {
        var uri = Navigation.Uri;
        foreach (var route in routes)
        {
            if (uri.Contains(route))
                return true;
        }
        return false;
    }

    protected string DrawerTitle => AppStateService.CurrentDrawerContent switch
    {
        Summary => "Sent",
        AuthorizeDapp => "Authorize App",
        Receive => AppStateService.IsReceiveAdvancedMode ? "Advanced Mode" : "Your Address",
        Send => AppStateService.IsSendConfirmed ? "Summary" : "Send Assets",
        SelectAsset => "Select Assets",
        TransactionStatus => "Transaction Sent",
        Settings => "Settings",
        NodeSettings => "Node Settings",
        Manage => ManageSection.IsManageAccountFormVisible
            ? (ManageSection.IsManageEditMode ? "Edit Wallet" : "New Account")
            : "Manage",
        _ => "Details"
    };

    protected void ToggleSidebar()
    {
        AppStateService.IsSidebarOpen = !AppStateService.IsSidebarOpen;
    }

    protected void HandleBackNavigation()
    {
        if (AppStateService.CurrentDrawerContent == SelectAsset)
        {
            AppStateService.SetDrawerContent(Send);
            AppStateService.RequestResetSendConfirmation();
        }
        else if (AppStateService.CurrentDrawerContent == Send)
        {
            AppStateService.RequestResetSendConfirmation();
        }
        else if (AppStateService.CurrentDrawerContent == NodeSettings)
        {
            AppStateService.SetDrawerContent(Settings);
        }
        else if (AppStateService.CurrentDrawerContent == Receive && AppStateService.IsReceiveAdvancedMode)
        {
            AppStateService.IsReceiveAdvancedMode = false;
        }
        else if (AppStateService.CurrentDrawerContent == Manage && ManageSection.IsManageAccountFormVisible)
        {
            ManageSection.HideAccountForm();
        }
        else
        {
            AppStateService.IsFilterDrawerOpen = false;
        }
    }

    protected void HandleAddRecipient()
    {
        AppStateService.RequestAddRecipient();
    }

    protected void HandleSettingsClick()
    {
        AppStateService.SetDrawerContent(Settings);
    }

    protected void HandleReceiveClick()
    {
        AppStateService.SetDrawerContent(Receive);
    }

    protected void HandleAdvancedModeToggle()
    {
        AppStateService.IsReceiveAdvancedMode = !AppStateService.IsReceiveAdvancedMode;
    }

    protected override void OnInitialized()
    {
        AppStateService.OnChanged += StateHasChanged;
        Navigation.LocationChanged += OnLocationChanged;
        ManageSection.OnManageStateChanged += StateHasChanged;

        SetDrawerContentForCurrentRoute();
    }

    protected void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        SetDrawerContentForCurrentRoute();
        StateHasChanged();
    }
    
    private void SetDrawerContentForCurrentRoute()
    {
        if (IsOnRoute(Routes.History))
        {
            AppStateService.CurrentDrawerContent = Summary;
        }
        else if (IsOnRoute(Routes.Dapp))
        {
            AppStateService.CurrentDrawerContent = AuthorizeDapp;
        }
        else
        {
            AppStateService.CurrentDrawerContent = None;
        }
    }

    public void Dispose()
    {
        AppStateService.OnChanged -= StateHasChanged;
        Navigation.LocationChanged -= OnLocationChanged;
        ManageSection.OnManageStateChanged -= StateHasChanged;
    }
}