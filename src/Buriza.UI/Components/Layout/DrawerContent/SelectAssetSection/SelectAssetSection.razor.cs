using Buriza.UI.Services;
using Buriza.UI.Data.Dummy;
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

    private string SelectedAsset { get; set; } = string.Empty; 

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