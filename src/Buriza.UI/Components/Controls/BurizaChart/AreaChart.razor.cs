using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Controls;

public partial class AreaChart : IAsyncDisposable
{
    [Inject]
    public required JavaScriptBridgeService JavaScriptBridgeService { get; set; }

    [Parameter]
    public double[] Data { get; set; } = [];

    [Parameter]
    public string Color { get; set; } = "#51DDAE";

    [Parameter]
    public string GradientStart { get; set; } = "rgba(81, 221, 174, 0.3)";

    [Parameter]
    public string GradientEnd { get; set; } = "rgba(81, 221, 174, 0)";

    [Parameter]
    public string Class { get; set; } = "";

    private string _canvasId = $"chart-{Guid.NewGuid():N}";
    private bool _isRendered;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender || !_isRendered)
        {
            await Task.Delay(50);
            await JavaScriptBridgeService.CreateAreaChartAsync(_canvasId, Data, Color, GradientStart, GradientEnd);
            _isRendered = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await JavaScriptBridgeService.DestroyChartAsync(_canvasId);
    }
}
