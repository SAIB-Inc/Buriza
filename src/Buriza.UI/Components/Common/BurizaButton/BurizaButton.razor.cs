using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace Buriza.UI.Components.Common;

public partial class BurizaButton
{
    [Parameter]
    public string Class { get; set; } = string.Empty;

    [Parameter]
    public string Href { get; set; } = string.Empty;

    [Parameter]
    public string StartIcon { get; set; } = string.Empty;

    [Parameter]
    public string EndIcon { get; set; } = string.Empty;

    [Parameter]
    public bool Disabled { get; set; } = false;

    [Parameter]
    public bool Ripple { get; set; } = true;

    [Parameter]
    public Color Color { get; set; } = Color.Default;

    [Parameter]
    public Variant Variant { get; set; } = Variant.Filled;

    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private string ButtonClass => $"""
        !rounded-full !normal-case !font-medium !text-[var(--mud-palette-background)] h-10 w-full !text-sm !shadow-none
        {GetColorClass()}
        {Class}
    """;

    private string GetColorClass() => Color switch
    {
        Color.Primary => "!bg-[var(--mud-palette-primary-lighten)] !text-[var(--mud-palette-primary-text)] hover:!bg-[var(--mud-palette-primary)] focus:!bg-[var(--mud-palette-primary-darken)]",
        Color.Secondary => "!bg-[var(--mud-palette-secondary-lighten)] !text-[var(--mud-palette-secondary-text)] hover:!bg-[var(--mud-palette-secondary)] focus:!bg-[var(--mud-palette-secondary-darken)]",
        _ => "bg-gray-300"
    };
}
