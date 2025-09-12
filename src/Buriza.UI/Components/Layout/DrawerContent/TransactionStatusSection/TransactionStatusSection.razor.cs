using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Layout;

public partial class TransactionStatusSection
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    private void HandleDone()
    {
        // Close the drawer when done
        AppStateService.IsFilterDrawerOpen = false;
    }
}