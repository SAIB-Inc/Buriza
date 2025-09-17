using Microsoft.JSInterop;

namespace Buriza.UI.Services;

public class JavaScriptBridgeService(IJSRuntime jSRuntime)
{
    public async Task<int> GetScreenWidthAsync()
    {
        return await jSRuntime.InvokeAsync<int>("getScreenWidth");
    }
}