using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Pages;

public partial class Components
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    private string selectedValue = "Item 1";
    private bool _drawerOpen = false;

    private Task OnValueChanged(string value)
    {
        selectedValue = value;
        return Task.CompletedTask;
    }
    
}
