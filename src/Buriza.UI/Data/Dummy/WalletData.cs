using Buriza.Data.Models.Common;
using Buriza.UI.Resources;

namespace Buriza.UI.Data.Dummy;

public static class WalletData
{
    public static List<Wallet> GetDummyWallets()
    {
        return
        [
            new Wallet
            {
                Id = 1,
                Name = "WalletName1",
                Avatar = Profiles.Profile1,
                Accounts =
                [
                    new WalletAccount { Name = "Account01", DerivationPath = "m/1852'/1815'/1'", Avatar = Profiles.Profile1, IsActive = true },
                    new WalletAccount { Name = "Account02", DerivationPath = "m/1852'/1815'/2'", Avatar = Profiles.Profile2, IsActive = false },
                    new WalletAccount { Name = "Account03", DerivationPath = "m/1852'/1815'/3'", Avatar = Profiles.Profile3, IsActive = true }
                ]
            },
            new Wallet
            {
                Id = 2,
                Name = "WalletName2",
                Avatar = Profiles.Profile2,
                Accounts =
                [
                    new WalletAccount { Name = "Account01", DerivationPath = "m/1852'/1815'/1'", Avatar = Profiles.Profile4, IsActive = false },
                    new WalletAccount { Name = "Account02", DerivationPath = "m/1852'/1815'/2'", Avatar = Profiles.Profile5, IsActive = true },
                    new WalletAccount { Name = "Account03", DerivationPath = "m/1852'/1815'/3'", Avatar = Profiles.Profile6, IsActive = false }
                ]
            },
            new Wallet
            {
                Id = 3,
                Name = "WalletName3",
                Avatar = Profiles.Profile3,
                Accounts =
                [
                    new WalletAccount { Name = "Account01", DerivationPath = "m/1852'/1815'/1'", Avatar = Profiles.Profile7, IsActive = true },
                    new WalletAccount { Name = "Account02", DerivationPath = "m/1852'/1815'/2'", Avatar = Profiles.Profile8, IsActive = true },
                    new WalletAccount { Name = "Account03", DerivationPath = "m/1852'/1815'/3'", Avatar = Profiles.Profile1, IsActive = false }
                ]
            }
        ];
    }
}
