using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using static Buriza.UI.Services.DrawerContentType;

namespace Buriza.UI.Components.Layout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
    [Inject]
    public required AppStateService AppStateService { get; set; }
    
    [Inject] 
    public required NavigationManager Navigation { get; set; }

    protected bool IsHeaderHidden => Navigation.Uri.Contains("/transaction/success") ||
                                   Navigation.Uri.Contains("/onboard") ||
                                   Navigation.Uri.Contains("/splash");

    protected string DrawerTitle => AppStateService.CurrentDrawerContent switch
    {
        Summary => "Sent",
        AuthorizeDapp => "Authorize App",
        Receive => IsReceiveAdvancedMode ? "Advanced Mode" : "Your Address",
        Send => IsSendConfirmed ? "Summary" : "Send Assets",
        SelectAsset => "Select Assets",
        TransactionStatus => "Transaction Sent",
        Settings => "Settings",
        NodeSettings => "Node Settings",
        Manage => ManageSection.IsManageAccountFormVisible 
            ? (ManageSection.IsManageEditMode ? "Edit Wallet" : "New Account")
            : "Manage",
        _ => "Details"
    };

    public static bool IsSendConfirmed { get; set; } = false;

    protected void ToggleSidebar()
    {
        AppStateService.IsSidebarOpen = !AppStateService.IsSidebarOpen;
    }

    protected void HandleBackNavigation()
    {
        if (AppStateService.CurrentDrawerContent == SelectAsset)
        {
            AppStateService.SetDrawerContent(Send);
            OnResetSendConfirmation?.Invoke();
        }
        else if (AppStateService.CurrentDrawerContent == Send)
        {
            OnResetSendConfirmation?.Invoke();
        }
        else if (AppStateService.CurrentDrawerContent == NodeSettings)
        {
            AppStateService.SetDrawerContent(Settings);
        }
        else if (AppStateService.CurrentDrawerContent == Receive && IsReceiveAdvancedMode)
        {
            SetReceiveAdvancedMode(false);
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
        OnAddRecipient?.Invoke();
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
        SetReceiveAdvancedMode(!IsReceiveAdvancedMode);
    }

    public static bool IsReceiveAdvancedMode { get; private set; } = false;
    public static Action? OnReceiveAdvancedModeChanged { get; set; }
    
    public static void SetReceiveAdvancedMode(bool isAdvanced)
    {
        IsReceiveAdvancedMode = isAdvanced;
        OnReceiveAdvancedModeChanged?.Invoke();
    }

    public static Action? OnAddRecipient { get; set; }
    public static Action? OnResetSendConfirmation { get; set; }
    
    public static void SetSendConfirmed(bool confirmed)
    {
        IsSendConfirmed = confirmed;
        // Trigger UI refresh for all MainLayout instances
        OnSendConfirmationChanged?.Invoke();
    }
    
    public static Action? OnSendConfirmationChanged { get; set; }

    public static MudTheme BurizaTheme => new()
    {
        Typography = new()
        {
            Default = new DefaultTypography()
            {
                FontFamily = ["Poppins", "sans-serif"]
            }
        },

        PaletteDark = new()
        {
            Background = "#10131B",
            BackgroundGray = "#0B0E15",
            Surface = "#0B0E15",
            OverlayDark = "#000000",
            OverlayLight = "#272A32",
            AppbarBackground = "#0E0E0E",
            PrimaryLighten = "#AEC6FF",
            Primary = "#4F8EFF",
            PrimaryDarken = "#9EB8F5",
            PrimaryContrastText = "#002E6B",
            SecondaryLighten = "#002E6B",
            Secondary = "#002456",
            SecondaryDarken = "#001A42",
            SecondaryContrastText = "#AEC6FF",
            Tertiary = "#00173D",
            TextPrimary = "#E0E2ED",
            TextSecondary = "#8490B1",
            GrayLighter = "#AFAFAF",
            GrayLight = "#2C2C2E",
            GrayDefault = "#272A32",
            GrayDark = "#C1C6D7",
            GrayDarker = "#272A32",
            DrawerBackground = "#181B23",
            DarkLighten = "#8B90A0",
            Dark = "#181B23",
            DarkDarken = "#1C1F27",
            DarkContrastText = "#272A32",
            Divider = "#414754",
            TableLines = "#23304B",
            SuccessLighten = "#71FAC9",
            Success = "#00B286",
            SuccessDarken = "#002116",
            Error = "#FF5449",
            Info = "#00B286",
            InfoLighten = "#1C1F27",
            Warning = "#FF9C39",
            LinesDefault = "#181B23",
            LinesInputs = "#1C1F27"
        },
        PaletteLight = new()
        {
            Background = "#FAF9FF",
            BackgroundGray = "#FFFFFF",
            Surface = "#ECEDF8",
            OverlayLight = "#FFFFFF",
            AppbarBackground = "#E0E2ED",
            PrimaryLighten = "#0057C0",
            Primary = "#227CFF",
            PrimaryContrastText = "#FFFFFF",
            SecondaryLighten = "#A6C1FE",
            Secondary = "#0057C0",
            SecondaryContrastText = "#324E83",
            Tertiary = "#FEFCFF",
            TextPrimary = "#181B23",
            TextSecondary = "#515E7C",
            GrayLighter = "#AFAFAF",
            GrayLight = "#FFFFFFCC",
            GrayDefault = "#E6E7F3",
            GrayDark = "#181B23",
            GrayDarker = "#D8D9E4",
            DrawerBackground = "#ECEDF8",
            DarkLighten = "#C1C6D7",
            Dark = "#FFFFFF",
            DarkDarken = "#E8EFFB",
            DarkContrastText = "#ECEDF8",
            Divider = "#C1C6D7",
            TableLines = "#B9C6E9",
            SuccessLighten = "#77FBAF",
            Success = "#00A663",
            SuccessDarken = "#005AC4",
            Error = "#BA1A1A",
            Info = "#51DDAE",
            InfoLighten= "#FFFFFF",
            Warning = "#FF9C39",
            LinesDefault = "#E6E7F3",
            LinesInputs = "#ECEDF8"
        }
    };

    protected override void OnInitialized()
    {
        AppStateService.OnChanged += StateHasChanged;
        Navigation.LocationChanged += OnLocationChanged;
        OnSendConfirmationChanged += StateHasChanged;
        OnReceiveAdvancedModeChanged += StateHasChanged;
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
        if (Navigation.Uri.Contains("/history"))
        {
            AppStateService.CurrentDrawerContent = Summary;
        }
        else if (Navigation.Uri.Contains("/dapp"))
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
        OnSendConfirmationChanged -= StateHasChanged;
        OnReceiveAdvancedModeChanged -= StateHasChanged;
        ManageSection.OnManageStateChanged -= StateHasChanged;
    }
}