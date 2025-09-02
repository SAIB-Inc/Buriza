using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Common;

public partial class BurizaHeader : IDisposable
{
    [Inject]
    public required NavigationManager Navigation { get; set; }
    
    private bool isSettingsVisible = false;
    
    protected override void OnInitialized()
    {
        Navigation.LocationChanged += HandleLocationChanged;
    }
    
    private void HandleLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        StateHasChanged();
    }
    
    private void ToggleSettings()
    {
        isSettingsVisible = !isSettingsVisible;
    }
    
    private string GetSpanClass(string route)
    {
        bool isActive = Navigation.Uri.Contains(route) || (route == "/" && (Navigation.Uri.EndsWith("/") || Navigation.Uri.Contains("/popup") || Navigation.Uri.Contains("/options") || Navigation.Uri.Contains("/index")));
        return isActive 
            ? "text-[11px] !text-[var(--mud-palette-primary-text)] !bg-[var(--mud-palette-primary-lighten)] !rounded-full !px-1 !py-[2px]"
            : "text-[11px] !text-[var(--mud-palette-text-secondary)] !px-1 !py-[2px]";
    }
    
    public void Dispose()
    {
        Navigation.LocationChanged -= HandleLocationChanged;
    }
}