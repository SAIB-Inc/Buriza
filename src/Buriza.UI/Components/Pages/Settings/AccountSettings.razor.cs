using Buriza.UI.Resources;

namespace Buriza.UI.Components.Pages;

public partial class AccountSettings
{
    protected int SelectedNftIconIndex { get; set; } = 0;

    protected List<string> NftIcons { get; set; } =
    [
        Profiles.Profile1, Profiles.Profile2, Profiles.Profile3, Profiles.Profile4,
        Profiles.Profile5, Profiles.Profile6, Profiles.Profile7, Profiles.Profile8,
    ];
}
