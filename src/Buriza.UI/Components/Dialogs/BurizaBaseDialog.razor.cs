using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Dialogs;

public partial class BurizaBaseDialog
{
    [Parameter]
    public string Class { get; set; } = string.Empty;

    [Parameter]
    public string TitleClass { get; set; } = string.Empty;

    [Parameter]
    public string ContentClass { get; set; } = string.Empty;

    [Parameter]
    public string ActionsClass { get; set; } = string.Empty;

    [Parameter]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public RenderFragment? DialogContent { get; set; }

    [Parameter]
    public RenderFragment? DialogActions { get; set; }

    [Parameter]
    public EventCallback OnClose { get; set; }

    private string DialogClass => $"!bg-[var(--mud-palette-background)] !rounded-[16px] !p-0 !shadow-none {Class}";

    private string DialogTitleClass => $"!p-4 {TitleClass}";

    private string DialogContentClass => $"!p-0 !overflow-y-auto !max-h-[70vh] {ContentClass}";

    private string DialogActionsClass => $"!px-4 !pb-4 !pt-4 {ActionsClass}";
}
