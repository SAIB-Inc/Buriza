using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Controls;

public partial class BurizaAssetCard
{
    [Parameter]
    public string AssetIcon { get; set; } = string.Empty;

    [Parameter]
    public string AssetName { get; set; } = string.Empty;

    [Parameter]
    public string Class { get; set; } = string.Empty;

    [Parameter]
    public float PricePercentage { get; set; }

    [Parameter]
    public float AssetAmount { get; set; }

    [Parameter]
    public float UsdConversion { get; set; }
}