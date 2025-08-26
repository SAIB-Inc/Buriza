using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Pages;

public partial class Home
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    private MudCarousel<object>? _carousel;
    private bool showCarousel = true;
    private bool IsLastSlide => _carousel != null && _carousel.SelectedIndex == _carousel.Items.Count - 1;

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