using Microsoft.JSInterop;

namespace Buriza.UI.Services;

public class JavaScriptBridgeService(IJSRuntime jSRuntime)
{
    public async Task<int> GetScreenWidthAsync()
    {
        return await jSRuntime.InvokeAsync<int>("getScreenWidth");
    }

    public async Task CreateAreaChartAsync(
        string canvasId,
        double[] data,
        string color,
        string gradientStart,
        string gradientEnd)
    {
        await jSRuntime.InvokeVoidAsync("createAreaChart", canvasId, data, color, gradientStart, gradientEnd);
    }

    public async Task DestroyChartAsync(string canvasId)
    {
        await jSRuntime.InvokeVoidAsync("destroyChart", canvasId);
    }

    public async Task CreateDualAreaChartAsync(
        string canvasId,
        double[] data1,
        string color1,
        string gradientStart1,
        string gradientEnd1,
        double[] data2,
        string color2,
        string gradientStart2,
        string gradientEnd2)
    {
        await jSRuntime.InvokeVoidAsync("createDualAreaChart", canvasId, data1, color1, gradientStart1, gradientEnd1, data2, color2, gradientStart2, gradientEnd2);
    }

    public async Task AttachWindowResizeEvent<T>(DotNetObjectReference<T> dotNetRef) where T : class
    {
        await jSRuntime.InvokeVoidAsync("attachWindowResizeEvent", dotNetRef);
    }

    public async Task DetachWindowResizeEvent<T>(DotNetObjectReference<T> dotNetRef) where T : class
    {
        await jSRuntime.InvokeVoidAsync("detachWindowResizeEvent", dotNetRef);
    }
}