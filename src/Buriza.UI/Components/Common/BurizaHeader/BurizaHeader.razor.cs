using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Buriza.UI.Components.Common;

public partial class BurizaHeader : IDisposable
{
    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected bool IsPopupVisible = false;

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
        AppStateService.OnChanged += OnStateChanged;
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        ClosePopup();
        await InvokeAsync(StateHasChanged);
    }

    private async void OnStateChanged()
    {
        await InvokeAsync(StateHasChanged);
    }

    protected void TogglePopup()
    {
        IsPopupVisible = !IsPopupVisible;
    }

    protected void ClosePopup()
    {
        IsPopupVisible = false;
    }

    protected bool IsOnRoute(string route)
    {
        var path = new Uri(Navigation.Uri).AbsolutePath;
        if (route == "/")
            return path == "/";
        return path.Contains(route);
    }

    protected string GetSpanClass(string route)
    {
        return IsOnRoute(route)
            ? "text-[11px] !text-[var(--mud-palette-primary-text)] !bg-[var(--mud-palette-primary-lighten)] !rounded-full !px-1 !py-[2px]"
            : "text-[11px] !text-[var(--mud-palette-text-secondary)] !px-1 !py-[2px]";
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        AppStateService.OnChanged -= OnStateChanged;
    }
}
