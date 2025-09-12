using Buriza.UI.Services;
using Buriza.UI.Data;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Layout;

public partial class SelectAssetSection : ComponentBase, IDisposable
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected List<TokenAsset> TokenAssets { get; set; } = [];
    protected List<NftAsset> NftAssets { get; set; } = [];

    protected override void OnInitialized()
    {
        AppStateService.OnChanged += StateHasChanged;
        TokenAssets = TokenAssetData.GetDummyTokenAssets();
        NftAssets = NftAssetData.GetDummyNftAssets();
    }

    public void Dispose()
    {
        AppStateService.OnChanged -= StateHasChanged;
    }
}