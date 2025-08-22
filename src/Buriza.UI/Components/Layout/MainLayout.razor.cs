using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Layout;

public partial class MainLayout
{
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
            SecondaryLighten = "#002E6B",
            Secondary = "#002456",
            SecondaryDarken = "#001A42",
            TextPrimary = "#E0E2ED",
            TextSecondary = "#8490B1",
            GrayLighter = "#AFAFAF",
            GrayLight = "#2C2C2E",
            GrayDefault = "#272A32",
            GrayDark = "#C1C6D7",
            GrayDarker = "#363942",
            Dark = "#181B23",
            TableLines = "#23304B",
            Success = "#00B286"
        },
        PaletteLight = new()
        {
            Background = "#FAF9FF",
            BackgroundGray = "#ECEDF8",
            AppbarBackground = "#E0E2ED",
            PrimaryLighten = "#AEC6FF",
            Primary = "#227CFF",
            Secondary = "#0057C0",
            TextPrimary = "#181B23",
            TextSecondary = "#515E7C",
            GrayDefault = "#272A32",
            GrayDark = "#181B23",
            GrayDarker = "#363942",
            Dark = "#ECEDF8",
            TableLines = "#B9C6E9",
            Success = "#00A663"
        }
    };
}