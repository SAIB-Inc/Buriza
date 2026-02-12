using Buriza.UI.Routing;
using Buriza.UI.Services;
using Buriza.Data.Models.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using MudBlazor;
using static Buriza.Data.Models.Enums.DrawerContentType;

namespace Buriza.UI.Components.Layout;

public partial class MainLayout : LayoutComponentBase, IAsyncDisposable
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required JavaScriptBridgeService JavaScriptBridgeService { get; set; }

    private DotNetObjectReference<MainLayout>? _dotNetRef;
    protected Anchor FilterDrawerAnchor { get; set; } = Anchor.End;

    protected bool IsHeaderHidden => IsOnRoute(Routes.TransactionSuccess, Routes.Onboard, Routes.Splash);

    private static MudTheme BurizaTheme => new()
    {
        Typography = new()
        {
            Default = new DefaultTypography()
            {
                FontFamily = ["Poppins", "sans-serif"]
            }
        },

        Shadows = new Shadow()
        {
            Elevation = new string[]
            {
                "none",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
                "0px 1px 3px 1px rgba(0, 0, 0, 0.15)"
            }
        },

        PaletteDark = new PaletteDark()
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
            TertiaryLighten = "#4D4D4D",
            TertiaryDarken = "#164174",
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
            TableLines = "#B9C6E9",
            SuccessLighten = "#71FAC9",
            Success = "#00B286",
            SuccessDarken = "#002116",
            SuccessContrastText = "#FAF9FF",
            Error = "#FF5449",
            ErrorLighten = "#F87171",
            Info = "#51DDAE",
            InfoLighten = "#1C1F27",
            InfoDarken = "#006FEF",
            InfoContrastText = "#D7E9FF",
            Warning = "#FF9C39",
            WarningLighten = "#D8E2FF",
            WarningDarken = "#242320",
            LinesDefault = "#181B23",
            LinesInputs = "#1C1F27",
            ActionDefault = "#2A2E38"
        },

        PaletteLight = new PaletteLight()
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
            TertiaryLighten = "#4D4D4D",
            TertiaryDarken = "#164174",
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
            SuccessContrastText = "#FAF9FF",
            Error = "#BA1A1A",
            Info = "#51DDAE",
            InfoLighten = "#FFFFFF",
            InfoDarken = "#006FEF",
            InfoContrastText = "#D7E9FF",
            WarningLighten = "#FFDD57",
            Warning = "#FF9C39",
            WarningDarken = "#242320",
            LinesDefault = "#E6E7F3",
            LinesInputs = "#ECEDF8"
        }
    };

    private bool IsOnRoute(params string[] routes)
    {
        var path = new Uri(Navigation.Uri).AbsolutePath;
        foreach (var route in routes)
        {
            if (route == "/" && path == "/")
                return true;
            if (route != "/" && path.Contains(route))
                return true;
        }
        return false;
    }

    protected string DrawerTitle => AppStateService.CurrentDrawerContent switch
    {
        Summary => AppStateService.SummaryTransactionType switch
        {
            TransactionType.Sent => "Sent",
            TransactionType.Received => "Received",
            TransactionType.Mixed => "Mixed",
            _ => "Summary"
        },
        AuthorizeDapp => "Authorize App",
        ConnectedApps => "Connected dApps (10)",
        Receive => AppStateService.IsReceiveAdvancedMode ? "Advanced Mode" : "Your Address",
        Send => AppStateService.IsSendConfirmed ? "Summary" : "Send Assets",
        SelectAsset => "Select Assets",
        TransactionStatus => "Transaction Sent",
        Settings => "Settings",
        NodeSettings => "Node Settings",
        SwitchWallet => "Switch Wallet (50)",
        SwitchAccount => AppStateService.SelectedWalletName,
        AccountSettings => "Account Settings",
        Manage => AppStateService.IsManageAccountFormVisible
            ? (AppStateService.IsManageEditMode ? "Edit Wallet" : "New Account")
            : "Manage",
        DrawerContentType.Filter => "Filter",
        _ => "Details"
    };

    protected static string GetCategoryLabel(TransactionCategory category) => category switch
    {
        TransactionCategory.Contract => "Contract",
        TransactionCategory.Mint => "Mint",
        TransactionCategory.Delegation => "Delegation",
        TransactionCategory.Withdrawal => "Withdrawal",
        TransactionCategory.Governance => "Governance",
        _ => ""
    };

    protected static string GetCategoryChipClass(TransactionCategory category) => category switch
    {
        TransactionCategory.Contract => "!bg-[var(--mud-palette-secondary-lighten)]",
        TransactionCategory.Mint => "!bg-[var(--mud-palette-secondary-darken)]",
        _ => "!bg-[var(--mud-palette-secondary)]"
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
        else if (AppStateService.CurrentDrawerContent == SwitchWallet)
        {
            AppStateService.SetDrawerContent(Settings);
        }
        else if (AppStateService.CurrentDrawerContent == SwitchAccount)
        {
            AppStateService.SetDrawerContent(Settings);
        }
        else if (AppStateService.CurrentDrawerContent == AccountSettings)
        {
            AppStateService.SetDrawerContent(Settings);
        }
        else if (AppStateService.CurrentDrawerContent == Receive && AppStateService.IsReceiveAdvancedMode)
        {
            AppStateService.IsReceiveAdvancedMode = false;
        }
        else if (AppStateService.CurrentDrawerContent == Manage && AppStateService.IsManageAccountFormVisible)
        {
            AppStateService.HideManageAccountForm();
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
        AppStateService.OnChanged += HandleStateChanged;
        Navigation.LocationChanged += OnLocationChanged;

        SetContentForCurrentRoute();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JavaScriptBridgeService.AttachWindowResizeEvent(_dotNetRef);
        }
    }

    [JSInvokable]
    public Task OnResize(int width)
    {
        FilterDrawerAnchor = width < 768 ? Anchor.Bottom : Anchor.End;
        _ = InvokeAsync(StateHasChanged);
        return Task.CompletedTask;
    }

    private async void HandleStateChanged()
    {
        await InvokeAsync(StateHasChanged);
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        SetContentForCurrentRoute();
        await InvokeAsync(StateHasChanged);
    }

    private void SetContentForCurrentRoute()
    {
        // Set drawer content
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

        // Set sidebar content
        if (IsOnRoute(Routes.Home))
        {
            AppStateService.CurrentSidebarContent = SidebarContentType.History;
        }
        else if (IsOnRoute(Routes.History))
        {
            AppStateService.CurrentSidebarContent = SidebarContentType.None;
        }
        else
        {
            AppStateService.CurrentSidebarContent = SidebarContentType.None;
        }
    }

    public async ValueTask DisposeAsync()
    {
        AppStateService.OnChanged -= HandleStateChanged;
        Navigation.LocationChanged -= OnLocationChanged;

        if (_dotNetRef != null)
        {
            await JavaScriptBridgeService.DetachWindowResizeEvent(_dotNetRef);
            _dotNetRef.Dispose();
        }
    }
}