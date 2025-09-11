using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Buriza.UI.Components.Pages;

public partial class Dapp
{
    [Inject]
    public required AppStateService AppStateService { get; set; }
    
    [Inject]
    public required IJSRuntime JSRuntime { get; set; }
    
    protected int ExpandedCard = 1;
    protected int ExpandedDesktopSlide = 0;
    protected bool ShowAuthorization = false;
    protected string SelectedCategory = "All";
    
    protected void ExpandCard(int cardIndex)
    {
        ExpandedCard = cardIndex;
    }
    
    protected void ExpandDesktopSlide(int slideIndex)
    {
        ExpandedDesktopSlide = slideIndex;
    }
    
    protected async Task OnDappCardClicked()
    {
        int screenWidth = await JSRuntime.InvokeAsync<int>("getScreenWidth");
        
        if (screenWidth >= 1024)
        {
            AppStateService.IsFilterDrawerOpen = true;
        }
        else
        {
            ShowAuthorization = true;
        }
    }
}