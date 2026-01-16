using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using static Buriza.Data.Models.Enums.DrawerContentType;

namespace Buriza.UI.Components.Layout;

public partial class RightSidebar : ComponentBase
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected void ToggleTheme()
    {
        AppStateService.IsDarkMode = !AppStateService.IsDarkMode;
    }

    protected void OpenSendDrawer()
    {
        AppStateService.SetDrawerContent(Send);
    }

    protected void OpenReceiveDrawer()
    {
        AppStateService.SetDrawerContent(Receive);
    }

    protected void OpenManageDrawer()
    {
        AppStateService.SetDrawerContent(Manage);
    }
}
