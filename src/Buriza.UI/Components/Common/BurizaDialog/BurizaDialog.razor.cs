using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Common;

public partial class BurizaDialog
{
    [Parameter]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public RenderFragment? DialogContent { get; set; }

    [Parameter]
    public RenderFragment? DialogActions { get; set; }

    [Parameter]
    public EventCallback OnClose { get; set; }
}
