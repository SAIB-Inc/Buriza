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
            Primary = "#227CFF",
            Secondary = "#AEC6FF",
            TextPrimary = "#E0E2ED",
            TextSecondary = "#8490B1"
        },
        PaletteLight = new()
        {
            Background = "#FAF9FF",
            BackgroundGray = "#ECEDF8",
            AppbarBackground = "#E0E2ED",
            Primary = "#227CFF",
            Secondary = "#0057C0",
            TextPrimary = "#181B23",
            TextSecondary = "#515E7C"
        }
    };
}