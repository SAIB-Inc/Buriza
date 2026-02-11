using Buriza.UI.Data.Dummy;
using Buriza.UI.Components.Dialogs;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Layout;

public partial class ConnectedAppsSection : ComponentBase
{
    [Inject]
    public required IDialogService DialogService { get; set; }

    protected List<DappInfo> ConnectedApps { get; set; } = [];

    protected override void OnInitialized()
    {
        ConnectedApps = DappData.GetAllDapps();
    }

    protected async Task OnDeleteDappClicked(DappInfo dapp)
    {
        DialogOptions options = new()
        {
            NoHeader = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
            BackdropClick = true
        };

        IDialogReference dialog = await DialogService.ShowAsync<DisconnectDappDialog>(null, options);
        DialogResult? result = await dialog.Result;

        if (result is { Canceled: false })
        {
            ConnectedApps.Remove(dapp);
            StateHasChanged();
        }
    }

    protected string GetDappTypeColor(string type) => type.ToLower() switch
    {
        "defi" => "var(--dapp-type-defi)",
        "nft" => "var(--dapp-type-nft)",
        "gaming" => "var(--dapp-type-gaming)",
        "governance" => "var(--dapp-type-governance)",
        "social" => "var(--dapp-type-social)",
        "bridge" => "var(--dapp-type-bridge)",
        "infrastructure" => "var(--dapp-type-infrastructure)",
        "realfi" => "var(--dapp-type-realfi)",
        _ => "var(--mud-palette-text-secondary)"
    };
}
