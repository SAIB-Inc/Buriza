using Buriza.UI.Services;
using Buriza.Data.Models.Enums;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    private bool IsCard2Expanded { get; set; } = false;
    private bool IsSearchExpanded { get; set; } = false;

    private void ToggleSearch()
    {
        IsSearchExpanded = !IsSearchExpanded;
    }

    private void ToggleCardExpansion()
    {
        IsCard2Expanded = !IsCard2Expanded;
    }

    private void OpenReceiveDrawer()
    {
        AppStateService.CurrentDrawerContent = DrawerContentType.Receive;
        AppStateService.IsFilterDrawerOpen = true;
    }

    private void OpenSendDrawer()
    {
        AppStateService.CurrentDrawerContent = DrawerContentType.Send;
        AppStateService.IsFilterDrawerOpen = true;
    }

    protected override void OnInitialized()
    {
        AppStateService.CurrentSidebarContent = SidebarContentType.History;
        AppStateService.OnChanged += StateHasChanged;
    }

    public void Dispose()
    {
        AppStateService.OnChanged -= StateHasChanged;
    }
}