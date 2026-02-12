using Buriza.UI.Resources;
using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using static Buriza.Data.Models.Enums.DrawerContentType;

namespace Buriza.UI.Components.Layout;

public partial class SwitchWalletSection : ComponentBase
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected List<WalletItem> Wallets { get; set; } =
    [
        new("Wallet 1", Profiles.Profile1, true),
        new("Wallet 2", Profiles.Profile2, false),
        new("Wallet 3", Profiles.Profile3, false),
        new("Wallet 4", Profiles.Profile4, false),
        new("Wallet 5", Profiles.Profile5, false),
        new("Wallet 6", Profiles.Profile6, false),
        new("Wallet 7", Profiles.Profile7, false),
        new("Wallet 8", Profiles.Profile8, false),
        new("Wallet 9", Profiles.Profile1, false),
        new("Wallet 10", Profiles.Profile2, false),
    ];

    protected void HandleWalletClick(string walletName)
    {
        AppStateService.SelectedWalletName = walletName;
        AppStateService.SetDrawerContent(SwitchAccount);
    }

    protected record WalletItem(string Name, string Image, bool IsCurrent);
}
