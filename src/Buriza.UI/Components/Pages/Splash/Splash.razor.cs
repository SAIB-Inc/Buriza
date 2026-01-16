using Buriza.UI.Services;
using Buriza.Data.Models.Enums;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Pages;

public partial class Splash
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    private MudCarousel<object>? _carousel;
    private bool showCarousel = true;
    private bool IsLastSlide => _carousel != null && _carousel.SelectedIndex == _carousel.Items.Count - 1;

    protected override void OnInitialized()
    {
        AppStateService.CurrentSidebarContent = SidebarContentType.None;
    }

    private void OnNextButtonClicked()
    {
        if (IsLastSlide)
        {
            showCarousel = false;
            return;
        }

        _carousel?.Next();
    }
}