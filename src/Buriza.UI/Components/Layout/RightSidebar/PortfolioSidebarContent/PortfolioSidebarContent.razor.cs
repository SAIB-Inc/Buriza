using Buriza.UI.Services;
using Buriza.UI.Data.Dummy;
using Buriza.Data.Models.Common;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Layout;

public partial class PortfolioSidebarContent : ComponentBase
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected List<TokenAsset> TokenAssets { get; set; } = new();
    protected List<NftAsset> NftAssets { get; set; } = new();

    protected override void OnInitialized()
    {
        TokenAssets = TokenAssetData.GetDummyTokenAssets();
        NftAssets = NftAssetData.GetDummyNftAssets();
    }
}
