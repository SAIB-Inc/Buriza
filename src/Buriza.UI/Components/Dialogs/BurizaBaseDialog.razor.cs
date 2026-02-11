using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Dialogs;

public partial class BurizaBaseDialog
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
