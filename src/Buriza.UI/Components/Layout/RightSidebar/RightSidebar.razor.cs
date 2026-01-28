using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using static Buriza.Data.Models.Enums.DrawerContentType;

namespace Buriza.UI.Components.Layout;

public partial class RightSidebar : ComponentBase
{
    [Inject]
    public required AppStateService AppStateService { get; set; }
}
