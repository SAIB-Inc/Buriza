using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Pages;

public partial class Receive
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected MudCarousel<object>? _carousel;

    protected int CurrentSlide { get; set; } = 0;
}
