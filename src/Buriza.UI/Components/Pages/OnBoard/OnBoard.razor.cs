using MudBlazor;
using Buriza.UI.Services;
using Buriza.Data.Models.Enums;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Pages;

public partial class OnBoard
{
    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected int PhraseLength { get; set; } = 24;
    protected MudCarousel<object>? _carousel;
    protected bool IsLastSlide => _carousel != null && _carousel.SelectedIndex == _carousel.Items.Count - 1;
    protected List<string> RecoveryPhrase = ["random", "quack", "taught", "light", "short", "hard", "help", "please", "earth", "cake", "control", "guard", "trust", "frost", "echo", "swim", "give", "seen", "eyes", "wade", "ice", "explain", "water", "geese"];
    
    private int _currentSlide = 0;
    protected int CurrentSlide 
    { 
        get => _currentSlide;
        set
        {
            if (_currentSlide != value)
            {
                _currentSlide = value;
                StateHasChanged();
            }
        }
    }

    protected string SecondaryButtonText => CurrentSlide switch
    {
        0 => "Import Using Seed Phrase",
        1 => "Copy to Clipboard",
        2 => "Paste from Clipboard",
        _ => string.Empty
    };

    protected string PrimaryButtonText => CurrentSlide switch
    {
        0 => "Create New Wallet",
        1 => "Next",
        2 => "Next",
        3 => "Save Wallet",
        4 => "Continue",
        5 => "Continue",
        _ => "Next"
    };

    protected bool ShowSecondaryButton => CurrentSlide <= 2;

    protected override void OnInitialized()
    {
        AppStateService.CurrentSidebarContent = SidebarContentType.None;
    }

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
    }

    private void OnSlideChanged(int newIndex)
    {
        CurrentSlide = newIndex;
    }

    private void OnNextButtonClicked()
    {
        if (IsLastSlide)
        {
            Navigation.NavigateTo("/");
        }
        else
        {
            _carousel?.Next();
            if (_carousel != null)
            {
                CurrentSlide = _carousel.SelectedIndex;
            }
        }
    }

    private void OnSecondaryButtonClicked()
    {
        // TODO: Implement secondary button actions
    }

    private void OnBackButtonClicked()
    {
        _carousel?.Previous();
        if (_carousel != null)
        {
            CurrentSlide = _carousel.SelectedIndex;
        }
    }
}