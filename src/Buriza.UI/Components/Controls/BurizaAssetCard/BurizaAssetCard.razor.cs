using Microsoft.AspNetCore.Components;
using Buriza.Core.Models.Enums;

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

    [Parameter]
    public AssetType AssetType { get; set; } = AssetType.Token;

    [Parameter]
    public string CollectionName { get; set; } = string.Empty;
}