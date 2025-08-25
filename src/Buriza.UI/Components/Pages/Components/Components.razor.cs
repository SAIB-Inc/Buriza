using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Pages;

public partial class Components
{
    private string selectedValue = "Item 1";

    private Task OnValueChanged(string value)
    {
        selectedValue = value;
        return Task.CompletedTask;
    }
}
