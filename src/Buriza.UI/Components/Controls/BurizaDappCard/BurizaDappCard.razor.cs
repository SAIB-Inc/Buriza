using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Controls;

public partial class BurizaDappCard
{
    [Parameter]
    public string DappTitle { get; set; } = string.Empty;

    [Parameter]
    public string DappType { get; set; } = string.Empty;

    [Parameter]
    public string DappIcon { get; set; } = string.Empty;

    [Parameter]
    public string DappClass { get; set; } = string.Empty;

    [Parameter]
    public string DappThemeSecondary { get; set; } = string.Empty;

    [Parameter]
    public string DappThemePrimary { get; set; } = string.Empty;
}