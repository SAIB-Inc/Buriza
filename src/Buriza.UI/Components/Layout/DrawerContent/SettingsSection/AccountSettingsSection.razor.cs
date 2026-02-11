using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Layout;

public partial class AccountSettingsSection : ComponentBase
{
    [Inject]
    public required AppStateService AppStateService { get; set; }
}
