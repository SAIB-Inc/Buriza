using MudBlazor;

namespace Buriza.UI.Services;

public static class BurizaTheme
{
    public static MudTheme Theme => new()
    {
        Typography = new()
        {
            Default = new DefaultTypography()
            {
                FontFamily = ["Poppins", "sans-serif"]
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
            InfoLighten = "#FFFFFF",
            Warning = "#FF9C39",
            LinesDefault = "#E6E7F3",
            LinesInputs = "#ECEDF8"
        }
    };
}
