using Buriza.UI.Resources;
using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Pages;

public partial class WalletAccounts
{
    [Inject]
    public required NavigationManager NavigationManager { get; set; }

    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected int SelectedWalletIconIndex { get; set; } = 0;

    protected List<string> WalletIcons { get; set; } =
    [
        Profiles.Profile1, Profiles.Profile2, Profiles.Profile3, Profiles.Profile4,
        Profiles.Profile5, Profiles.Profile6, Profiles.Profile7, Profiles.Profile8,
    ];

    protected List<AccountItem> Accounts { get; set; } =
    [
        new("Account 1", Profiles.Profile1, "addr1j...dfsph9", "m/1852'/1815'/1'"),
        new("Account 2", Profiles.Profile2, "addr1q...k8m2x3", "m/1852'/1815'/2'"),
        new("Account 3", Profiles.Profile3, "addr1v...p7n4w1", "m/1852'/1815'/3'"),
        new("Account 4", Profiles.Profile4, "addr1x...r3t6y8", "m/1852'/1815'/4'"),
        new("Account 5", Profiles.Profile5, "addr1m...s5v9z2", "m/1852'/1815'/5'"),
    ];

    protected void HandleAccountSettingsClick()
    {
        NavigationManager.NavigateTo("/settings/account");
    }

    protected record AccountItem(string Name, string Image, string Address, string DerivationPath);
}
