using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using static Buriza.UI.Services.DrawerContentType;

namespace Buriza.UI.Components.Layout;

public partial class SettingsSection : ComponentBase
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected void HandleNodeClick()
    {
        AppStateService.SetDrawerContent(NodeSettings);
    }
}