using Microsoft.JSInterop;

namespace Buriza.UI.Services;

public class JavaScriptBridgeService(IJSRuntime jSRuntime)
{
    public async Task<int> GetScreenWidthAsync()
    {
        return await jSRuntime.InvokeAsync<int>("getScreenWidth");
    }

    public async Task CopyToClipboardAsync(string text)
    {
        await jSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
    }

    public async Task<string> ReadFromClipboardAsync()
    {
        return await jSRuntime.InvokeAsync<string>("navigator.clipboard.readText");
    }
}