using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Layout;

public partial class MainLayout
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

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
            TextPrimary = "#E0E2ED",
            TextSecondary = "#8490B1",
            GrayLighter = "#AFAFAF",
            GrayLight = "#2C2C2E",
            GrayDefault = "#272A32",
            GrayDark = "#C1C6D7",
            GrayDarker = "#363942",
            DarkLighten = "#8B90A0",
            Dark = "#181B23",
            TableLines = "#23304B",
            Success = "#00B286"
        },
        PaletteLight = new()
        {
            Background = "#FAF9FF",
            BackgroundGray = "#ECEDF8",
            AppbarBackground = "#E0E2ED",
            PrimaryLighten = "#0057C0",
            Primary = "#227CFF",
            PrimaryContrastText = "#FFFFFF",
            SecondaryLighten = "#A6C1FE",
            Secondary = "#0057C0",
            SecondaryContrastText = "#324E83",
            TextPrimary = "#181B23",
            TextSecondary = "#515E7C",
            GrayLighter = "#AFAFAF",
            GrayLight = "#0057C014",
            GrayDefault = "#FFFFFF",
            GrayDark = "#181B23",
            GrayDarker = "#E0E2ED",
            DarkLighten = "#C1C6D7",
            Dark = "#ECEDF8",
            TableLines = "#B9C6E9",
            Success = "#00A663"
        }
    };

    protected override void OnInitialized()
    {
        AppStateService.OnChanged += StateHasChanged;
    }

    public void Dispose()
    {
        AppStateService.OnChanged -= StateHasChanged;
    }
}