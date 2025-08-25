using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Common;

public partial class BurizaSelect<TItem>
{
    [Parameter]
    public required RenderFragment ChildContent { get; set; }

    [Parameter]
    public required DropdownWidth RelativeWidth { get; set; } = DropdownWidth.Relative;

    [Parameter]
    public required Adornment Adornment { get; set; } = Adornment.End;

    [Parameter]
    public Variant Variant { get; set; }

    [Parameter]
    public string Class { get; set; } = string.Empty;

    [Parameter]
    public string PopoverClass { get; set; } = string.Empty;

    [Parameter]
    public string AdornmentIcon { get; set; } = string.Empty;

    [Parameter]
    public TItem? Value { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public EventCallback<TItem> ValueChanged { get; set; }
    
    [Parameter]
    public Func<TItem, string>? ToStringFunc { get; set; }
}