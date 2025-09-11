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

    protected async Task HandleTransactionClick()
    {
        var screenWidth = await JSRuntime.InvokeAsync<int>("getScreenWidth");
        
        if (screenWidth >= 1024)
        {
            AppStateService.IsFilterDrawerOpen = true;
        }
        else
        {
            IsSummaryDisplayed = true;
        }
    }
}