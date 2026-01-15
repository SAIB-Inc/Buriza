using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Buriza.UI.Resources;
using MudBlazor;

namespace Buriza.UI.Data.Dummy;

public static class ReceiveAccountData
{
    public static List<ReceiveAccount> GetDummyReceiveAccounts()
    {
        return
        [
            new ReceiveAccount(
                Index: 0,
                AvatarUrl: Misc.Avatar,
                Name: "Account1",
                Address: "addr1q9g5p5kz0t9v8f7s6l3d4h8j2k0y5m5c4a7w5p2x9u8z7y5n2qz8f6j3h0a",
                DerivationPath: "m/1852'/1815'/1'",
                Status: AccountStatusType.Used
            ),
            new ReceiveAccount(
                Index: 1,
                AvatarUrl: Misc.Avatar,
                Name: "Account2",
                Address: "addr1q9g5p5kz0t9v8f7s6l3d4h8j2k0y5m5c4a7w5p2x9u8z7y5n2qz8f6j3h0a",
                DerivationPath: "m/1852'/1815'/2'",
                Status: AccountStatusType.Balance,
                Balance: 12.021m
            ),
            new ReceiveAccount(
                Index: 2,
                AvatarUrl: Misc.Avatar,
                Name: "Account3",
                Address: "addr1q9g5p5kz0t9v8f7s6l3d4h8j2k0y5m5c4a7w5p2x9u8z7y5n2qz8f6j3h0a",
                DerivationPath: "m/1852'/1815'/3'",
                Status: AccountStatusType.Unused
            ),
            new ReceiveAccount(
                Index: 3,
                AvatarUrl: Misc.Avatar,
                Name: "Account4",
                Address: "addr1q9g5p5kz0t9v8f7s6l3d4h8j2k0y5m5c4a7w5p2x9u8z7y5n2qz8f6j3h0a",
                DerivationPath: "m/1852'/1815'/4'",
                Status: AccountStatusType.Unused
            )
        ];
    }

    public static string GetChipColor(AccountStatusType status)
    {
        return status switch
        {
            AccountStatusType.Used => "!bg-[var(--mud-palette-primary)]",
            AccountStatusType.Unused => "!bg-[var(--mud-palette-warning)]",
            AccountStatusType.Balance => "!bg-[var(--mud-palette-info)]",
            _ => ""
        };
    }

    public static string GetChipIcon(AccountStatusType status)
    {
        return status switch
        {
            AccountStatusType.Used => BurizaIcons.CheckOutlinedIcon,
            AccountStatusType.Unused => Icons.Material.Outlined.Circle,
            AccountStatusType.Balance => BurizaIcons.AdaSymbolIcon,
            _ => ""
        };
    }
}