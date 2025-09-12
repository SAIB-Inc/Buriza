using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Controls;

public partial class BurizaSearchBar
{
    [Parameter]
    public string Class { get; set; } = string.Empty;
}