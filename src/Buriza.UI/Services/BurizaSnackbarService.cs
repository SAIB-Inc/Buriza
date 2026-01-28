using Buriza.UI.Components.Controls;
using MudBlazor;

namespace Buriza.UI.Services;

public class BurizaSnackbarService(ISnackbar Snackbar)
{
    private const int DefaultDuration = 3000;

    public void Show(string message, Severity severity, int? duration = null)
    {
        var actualDuration = duration ?? DefaultDuration;

        Snackbar.Add<BurizaSnackbar>(new Dictionary<string, object>
        {
            { nameof(BurizaSnackbar.Message), message },
            { nameof(BurizaSnackbar.Severity), severity },
            { nameof(BurizaSnackbar.Duration), actualDuration }
        }, Severity.Normal, config =>
        {
            config.HideIcon = true;
            config.ShowCloseIcon = false;
            config.VisibleStateDuration = actualDuration;
            config.SnackbarTypeClass = "!p-0 !shadow-lg [&_.mud-snackbar-content-message]:py-0!";
        });
    }
}
