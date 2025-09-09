using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Pages;

public partial class Home : IDisposable
{
    [Inject]
    public required AppStateService AppStateService { get; set; }
    
    private bool IsCard2Expanded { get; set; } = false;
    
    private void ToggleCardExpansion()
    {
        IsCard2Expanded = !IsCard2Expanded;
    }

    protected override void OnInitialized()
    {
        AppStateService.OnChanged += StateHasChanged;
    }

    public void Dispose()
    {
        AppStateService.OnChanged -= StateHasChanged;
    }
}