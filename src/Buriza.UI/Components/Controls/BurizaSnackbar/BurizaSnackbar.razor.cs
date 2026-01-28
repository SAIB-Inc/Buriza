using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Controls;

public partial class BurizaSnackbar : IDisposable
{
    [CascadingParameter]
    private MudSnackbarElement? SnackbarElement { get; set; }

    [Parameter]
    public string Message { get; set; } = string.Empty;

    [Parameter]
    public Severity Severity { get; set; } = Severity.Normal;

    [Parameter]
    public int Duration { get; set; } = 3000;

    private int _progress = 0;
    private System.Timers.Timer? _timer;
    private bool _disposed;

    protected override void OnInitialized()
    {
        if (Duration > 0)
        {
            var interval = Duration / 100.0;
            _timer = new System.Timers.Timer(interval);
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
        }
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_progress < 100)
        {
            _progress++;
            InvokeAsync(StateHasChanged);
        }
        else
        {
            _timer?.Stop();
        }
    }

    private void Close()
    {
        SnackbarElement?.Snackbar?.Dispose();
    }

    private string GetContainerClass()
    {
        return "relative rounded-[4px] px-3 py-4 min-w-[300px] bg-[var(--mud-palette-drawer-background)]";
    }

    private string GetProgressBarClass()
    {
        var baseClass = "h-full transition-all duration-100 ease-linear";
        return Severity switch
        {
            Severity.Success => $"{baseClass} bg-[var(--mud-palette-success)]",
            Severity.Error => $"{baseClass} bg-[var(--mud-palette-error)]",
            Severity.Info => $"{baseClass} bg-[var(--mud-palette-primary)]",
            Severity.Warning => $"{baseClass} bg-[var(--mud-palette-warning)]",
            _ => $"{baseClass} bg-[var(--mud-palette-primary)]"
        };
    }

    private string GetProgressBarStyle()
    {
        return $"width: {_progress}%";
    }

    private string GetIcon()
    {
        return Severity switch
        {
            Severity.Success => Icons.Material.Filled.CheckCircle,
            Severity.Error => Icons.Material.Filled.Error,
            Severity.Info => Icons.Material.Filled.Info,
            Severity.Warning => Icons.Material.Filled.Warning,
            _ => Icons.Material.Filled.Info
        };
    }

    private string GetIconClass()
    {
        var baseClass = "!text-xl";
        return Severity switch
        {
            Severity.Success => $"{baseClass} !text-[var(--mud-palette-success)]",
            Severity.Error => $"{baseClass} !text-[var(--mud-palette-error)]",
            Severity.Info => $"{baseClass} !text-[var(--mud-palette-primary)]",
            Severity.Warning => $"{baseClass} !text-[var(--mud-palette-warning)]",
            _ => baseClass
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _timer?.Stop();
            _timer?.Dispose();
            _disposed = true;
        }
    }
}
