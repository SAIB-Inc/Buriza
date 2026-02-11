using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Common;

public partial class BurizaPagination
{
    [Parameter]
    public int Count { get; set; }

    [Parameter]
    public int Selected { get; set; }

    [Parameter]
    public EventCallback<int> SelectedChanged { get; set; }
}
