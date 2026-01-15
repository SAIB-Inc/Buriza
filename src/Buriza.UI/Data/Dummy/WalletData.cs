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
                Avatar = Misc.Avatar,
                Accounts = 
                [
                    new WalletAccount { Name = "Account01", DerivationPath = "m/1852'/1815'/1'", Avatar = Misc.Avatar, IsActive = true },
                    new WalletAccount { Name = "Account02", DerivationPath = "m/1852'/1815'/2'", Avatar = Misc.Avatar, IsActive = false },
                    new WalletAccount { Name = "Account03", DerivationPath = "m/1852'/1815'/3'", Avatar = Misc.Avatar, IsActive = true }
                ]
            },
            new Wallet
            {
                Id = 2, 
                Name = "WalletName2",
                Avatar = Misc.Avatar,
                Accounts =
                [
                    new WalletAccount { Name = "Account01", DerivationPath = "m/1852'/1815'/1'", Avatar = Misc.Avatar, IsActive = false },
                    new WalletAccount { Name = "Account02", DerivationPath = "m/1852'/1815'/2'", Avatar = Misc.Avatar, IsActive = true },
                    new WalletAccount { Name = "Account03", DerivationPath = "m/1852'/1815'/3'", Avatar = Misc.Avatar, IsActive = false }
                ]
            },
            new Wallet
            {
                Id = 3,
                Name = "WalletName3", 
                Avatar = Misc.Avatar,
                Accounts =
                [
                    new WalletAccount { Name = "Account01", DerivationPath = "m/1852'/1815'/1'", Avatar = Misc.Avatar, IsActive = true },
                    new WalletAccount { Name = "Account02", DerivationPath = "m/1852'/1815'/2'", Avatar = Misc.Avatar, IsActive = true },
                    new WalletAccount { Name = "Account03", DerivationPath = "m/1852'/1815'/3'", Avatar = Misc.Avatar, IsActive = false }
                ]
            }
        ];
    }
}