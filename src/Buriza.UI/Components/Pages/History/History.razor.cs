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

    protected string GetRelativeTime(DateTime timestamp)
    {
        TimeSpan diff = DateTime.Now - timestamp;

        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} minutes ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} hours ago";
        if (diff.TotalDays < 30)
            return $"{(int)diff.TotalDays} days ago";

        return timestamp.ToString("MMM dd, yyyy");
    }

    protected async Task HandleTransactionClick()
    {
        int screenWidth = await JSRuntime.InvokeAsync<int>("getScreenWidth");

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