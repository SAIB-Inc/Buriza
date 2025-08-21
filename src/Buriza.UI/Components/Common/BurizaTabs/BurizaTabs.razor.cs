using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Common;

public partial class BurizaTabs
{
    [Parameter]
    public string? Class { get; set; } = string.Empty;

    [Parameter]
    public int ActivePanelIndex { get; set; } = 0;

    [Parameter]
    public EventCallback<int> ActivePanelIndexChanged { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}