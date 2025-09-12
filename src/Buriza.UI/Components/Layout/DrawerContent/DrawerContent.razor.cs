using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Layout;

public partial class DrawerContent : IDisposable
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected override void OnInitialized()
    {
        AppStateService.OnChanged += StateHasChanged;
    }

    public void Dispose()
    {
        AppStateService.OnChanged -= StateHasChanged;
    }
}