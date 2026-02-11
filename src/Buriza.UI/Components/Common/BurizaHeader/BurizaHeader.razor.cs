using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using static Buriza.Data.Models.Enums.DrawerContentType;

namespace Buriza.UI.Components.Common;

public partial class BurizaHeader : IDisposable
{
    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected bool IsPopupVisible = false;
    protected bool IsBaseLinksVisible = false;

    private DateTime _lastTapTime = DateTime.MinValue;
    private const int DoubleTapThresholdMs = 300;
    private System.Threading.Timer? _singleTapTimer;

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
        AppStateService.OnChanged += OnStateChanged;
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        CloseAll();
        await InvokeAsync(StateHasChanged);
    }

    private async void OnStateChanged()
    {
        await InvokeAsync(StateHasChanged);
    }

    protected void ToggleTheme()
    {
        AppStateService.IsDarkMode = !AppStateService.IsDarkMode;
    }

    protected void HandleBurizaTap()
    {
        DateTime now = DateTime.Now;
        double timeSinceLastTap = (now - _lastTapTime).TotalMilliseconds;

        // Cancel any pending single tap action
        _singleTapTimer?.Dispose();
        _singleTapTimer = null;

        if (timeSinceLastTap < DoubleTapThresholdMs)
        {
            // Double tap detected
            _lastTapTime = DateTime.MinValue;
            HandleDoubleTap();
        }
        else
        {
            // Potential single tap - wait to see if another tap comes
            _lastTapTime = now;
            _singleTapTimer = new System.Threading.Timer(
                _ => InvokeAsync(() =>
                {
                    HandleSingleTap();
                    StateHasChanged();
                }),
                null,
                DoubleTapThresholdMs,
                System.Threading.Timeout.Infinite
            );
        }
    }

    private void HandleSingleTap()
    {
        if (IsPopupVisible)
        {
            // If popup is open, close it and show base links
            IsPopupVisible = false;
            IsBaseLinksVisible = true;
        }
        else
        {
            // Toggle base links
            IsBaseLinksVisible = !IsBaseLinksVisible;
        }
    }

    private void HandleDoubleTap()
    {
        if (IsPopupVisible)
        {
            // Close everything - back to just logo
            IsPopupVisible = false;
            IsBaseLinksVisible = false;
        }
        else
        {
            // Open popup menu
            IsPopupVisible = true;
            IsBaseLinksVisible = false;
        }
    }

    protected void TogglePopup()
    {
        IsPopupVisible = !IsPopupVisible;
    }

    protected void ClosePopup()
    {
        IsPopupVisible = false;
    }

    protected void CloseAll()
    {
        IsPopupVisible = false;
        IsBaseLinksVisible = false;
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

    protected void OpenManageDrawer()
    {
        AppStateService.SetDrawerContent(Manage);
    }
    public void Dispose()
    {
        _singleTapTimer?.Dispose();
        Navigation.LocationChanged -= OnLocationChanged;
        AppStateService.OnChanged -= OnStateChanged;
    }
}
