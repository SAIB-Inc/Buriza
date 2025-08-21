using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Layout;

public partial class MainLayout
{
    public MudTheme BurizaTheme => new()
    {
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
            GrayDefault = "#272A32",
            Dark = "#181B23",
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
            Dark = "#ECEDF8"
        }
    };
}