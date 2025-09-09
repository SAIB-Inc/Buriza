using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace Buriza.UI.Components.Layout;

public partial class MainLayout : IDisposable
{
    [Inject]
    public required AppStateService AppStateService { get; set; }
    
    [Inject] 
    public required NavigationManager Navigation { get; set; }

    protected bool IsHeaderHidden => Navigation.Uri.Contains("/transaction/success") ||
                                   Navigation.Uri.Contains("/onboard") ||
                                   Navigation.Uri.Contains("/splash");

    protected string SidebarTitle => Navigation.Uri.Contains("/history") ? "Portfolio" : "History";
    protected bool ShowTabs => Navigation.Uri.Contains("/history");

    protected void ToggleSidebar()
    {
        AppStateService.IsSidebarOpen = !AppStateService.IsSidebarOpen;
    }

    protected void ToggleTheme()
    {
        AppStateService.IsDarkMode = !AppStateService.IsDarkMode;
    }

    protected void ToggleFilterDrawer()
    {
        AppStateService.IsFilterDrawerOpen = !AppStateService.IsFilterDrawerOpen;
    }

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
            GrayDarker = "#363942",
            DrawerBackground = "#1C1F27",
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
            Warning = "#FF9C39"
        },
        PaletteLight = new()
        {
            Background = "#FAF9FF",
            BackgroundGray = "#FFFFFF",
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
            GrayLight = "#0057C014",
            GrayDefault = "#FFFFFF",
            GrayDark = "#181B23",
            GrayDarker = "#E0E2ED",
            DrawerBackground = "#FFFFFF",
            DarkLighten = "#C1C6D7",
            Dark = "#E0E2ED",
            DarkDarken = "#E8EFFB",
            DarkContrastText = "#ECEDF8",
            Divider = "#C1C6D7",
            TableLines = "#B9C6E9",
            SuccessLighten = "#77FBAF",
            Success = "#00A663",
            SuccessDarken = "#005AC4",
            Error = "#BA1A1A",
            Info = "#51DDAE",
            Warning = "#FF9C39"
        }
    };

    protected override void OnInitialized()
    {
        AppStateService.OnChanged += StateHasChanged;
        Navigation.LocationChanged += OnLocationChanged;
    }

    protected void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        StateHasChanged();
    }

    public void Dispose()
    {
        AppStateService.OnChanged -= StateHasChanged;
        Navigation.LocationChanged -= OnLocationChanged;
    }
}