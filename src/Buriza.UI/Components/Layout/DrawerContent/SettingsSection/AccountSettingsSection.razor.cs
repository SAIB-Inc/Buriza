using Buriza.UI.Resources;
using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Layout;

public partial class AccountSettingsSection : ComponentBase
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected int SelectedNftIconIndex { get; set; } = 0;

    protected List<string> NftIcons { get; set; } =
    [
        Profiles.Profile1, Profiles.Profile2, Profiles.Profile3, Profiles.Profile4,
        Profiles.Profile5, Profiles.Profile6, Profiles.Profile7, Profiles.Profile8,
    ];
}
