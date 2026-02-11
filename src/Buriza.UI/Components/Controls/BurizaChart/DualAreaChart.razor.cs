using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Controls;

public partial class DualAreaChart : IAsyncDisposable
{
    [Inject]
    public required JavaScriptBridgeService JavaScriptBridgeService { get; set; }

    [Parameter]
    public double[] Data1 { get; set; } = [];

    [Parameter]
    public string Color1 { get; set; } = "#0057C0";

    [Parameter]
    public string GradientStart1 { get; set; } = "rgba(15, 96, 255, 0.16)";

    [Parameter]
    public string GradientEnd1 { get; set; } = "rgba(15, 96, 255, 0)";

    [Parameter]
    public double[] Data2 { get; set; } = [];

    [Parameter]
    public string Color2 { get; set; } = "#A6C1FE";

    [Parameter]
    public string GradientStart2 { get; set; } = "rgba(15, 183, 255, 0.08)";

    [Parameter]
    public string GradientEnd2 { get; set; } = "rgba(15, 183, 255, 0)";

    [Parameter]
    public string Class { get; set; } = "";

    private string _canvasId = $"chart-{Guid.NewGuid():N}";
    private bool _isRendered;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender || !_isRendered)
        {
            await Task.Delay(50);
            await JavaScriptBridgeService.CreateDualAreaChartAsync(
                _canvasId,
                Data1, Color1, GradientStart1, GradientEnd1,
                Data2, Color2, GradientStart2, GradientEnd2);
            _isRendered = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await JavaScriptBridgeService.DestroyChartAsync(_canvasId);
    }
}
