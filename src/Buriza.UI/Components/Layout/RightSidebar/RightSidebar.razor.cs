using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using static Buriza.UI.Services.DrawerContentType;

namespace Buriza.UI.Components.Layout;

public partial class RightSidebar
{
    [Inject]
    public required AppStateService AppStateService { get; set; }
    
    [Inject] 
    public required NavigationManager Navigation { get; set; }

    protected string SidebarTitle => Navigation.Uri.Contains("/history") ? "Portfolio" : "History";
    protected bool ShowTabs => Navigation.Uri.Contains("/history");

    protected void ToggleTheme()
    {
        AppStateService.IsDarkMode = !AppStateService.IsDarkMode;
    }

    protected void OpenSendDrawer()
    {
        AppStateService.SetDrawerContent(Send);
    }
}