using Buriza.UI.Resources;
using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Pages;

public partial class Receive
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected MudCarousel<object>? _carousel;

    private int _selectedAccountIndex = 0;
    private Account SelectedAccount => Accounts[_selectedAccountIndex];

    private enum AccountStatus
    {
        Used,
        Unused,
        Balance
    }

    private class Account
    {
        public int Index { get; set; }
        public string AvatarUrl { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        private string _address = string.Empty;
        public string Address
        {
            get => _address;
            set
            {
                _address = value;
                SplitAddress();
            }
        }

        public string DerivationPath { get; set; } = String.Empty;

        public AccountStatus Status { get; set; }
        public decimal Balance { get; set; }

        public string Start { get; set; } = String.Empty;
        public string Middle { get; set; } = String.Empty;
        public string End { get; set; } = String.Empty;


        public void SplitAddress(int numChars = 6)
        {
            if (string.IsNullOrEmpty(Address))
            {
                Start = Middle = End = "";
                return;
            }

            if (Address.Length <= numChars * 2)
            {
                Start = Address;
                Middle = "";
                End = "";
                return;
            }

            Start = Address[..numChars];
            End = Address[^numChars..];
            Middle = Address[numChars..^numChars];
        }
    }

    private List<Account> Accounts =
    [
        new Account
        {
            Index = 0,
            AvatarUrl = Misc.Avatar,
            Name = "Account1",
            Address = "addr1q9g5p5kz0t9v8f7s6l3d4h8j2k0y5m5c4a7w5p2x9u8z7y5n2qz8f6j3h0a",
            DerivationPath = "m/1852’/1815’/1’",
            Status = AccountStatus.Used
        },
        new Account
        {
            Index = 1,
            AvatarUrl = Misc.Avatar,
            Name = "Account2",
            DerivationPath = "m/1852’/1815’/2’",
            Address = "addr1q9g5p5kz0t9v8f7s6l3d4h8j2k0y5m5c4a7w5p2x9u8z7y5n2qz8f6j3h0a",
            Status = AccountStatus.Balance,
            Balance = 12.021m
        },
        new Account
        {
            Index = 2,
            AvatarUrl = Misc.Avatar,
            Name = "Account3",
            Address = "addr1q9g5p5kz0t9v8f7s6l3d4h8j2k0y5m5c4a7w5p2x9u8z7y5n2qz8f6j3h0a",
            DerivationPath = "m/1852’/1815’/3’",
            Status = AccountStatus.Unused
        },
        new Account
        {
            Index = 2,
            AvatarUrl = Misc.Avatar,
            Name = "Account3",
            Address = "addr1q9g5p5kz0t9v8f7s6l3d4h8j2k0y5m5c4a7w5p2x9u8z7y5n2qz8f6j3h0a",
            DerivationPath = "m/1852’/1815’/3’",
            Status = AccountStatus.Unused
        }
    ];

    private void OnAccountCardClicked(int index)
    {
        _selectedAccountIndex = index;
        _carousel?.Previous();
    }

    private void OnAdvancedModeButtonClicked()
    {
        _carousel?.Next();
    }

    private void OnBackButtonClicked()
    {
        _carousel?.Previous();
    }

    private static string GetChipColor(AccountStatus status)
    {
        return status switch
        {
            AccountStatus.Used => "!bg-[var(--mud-palette-primary)]",
            AccountStatus.Unused => "!bg-[var(--mud-palette-warning)]",
            AccountStatus.Balance => "!bg-[var(--mud-palette-info)]",
            _ => ""
        };
    }

    private static string GetChipIcon(AccountStatus status)
    {
        return status switch
        {
            AccountStatus.Used => BurizaIcons.CheckOutlinedIcon,
            AccountStatus.Unused => BurizaIcons.AdaSymbolIcon,
            AccountStatus.Balance => BurizaIcons.AdaSymbolIcon,
            _ => ""
        };
    }

}
