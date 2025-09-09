using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Buriza.UI.Components.Pages;

public partial class History
{
    [Inject]
    public required AppStateService AppStateService { get; set; }
    
    [Inject]
    public required IJSRuntime JSRuntime { get; set; }
    
    protected bool IsSummaryDisplayed { get; set; }
    
    protected void ToggleFilterDrawer()
    {
        AppStateService.IsFilterDrawerOpen = !AppStateService.IsFilterDrawerOpen;
    }

    protected async Task HandleTransactionClick()
    {
        // Check screen width
        var screenWidth = await JSRuntime.InvokeAsync<int>("getScreenWidth");
        
        if (screenWidth >= 1024) // lg breakpoint
        {
            // Desktop: Only open drawer
            AppStateService.IsFilterDrawerOpen = true;
        }
        else
        {
            // Mobile: Show summary content
            IsSummaryDisplayed = true;
        }
    }
}