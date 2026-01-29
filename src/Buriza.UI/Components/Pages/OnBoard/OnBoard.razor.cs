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

    [Inject]
    public required BurizaSnackbarService BurizaSnackbar { get; set; }

    [Inject]
    public required JavaScriptBridgeService JsBridge { get; set; }

    private int _phraseLength = 24;
    private bool _isPhraseTransitioning = false;
    protected bool IsPhraseTransitioning => _isPhraseTransitioning;


    protected int PhraseLength
    {
        get => _phraseLength;
        set
        {
            if (_phraseLength != value)
            {
                _phraseLength = value;
                _ = TransitionPhraseAsync();
            }
        }
    }

    private async Task TransitionPhraseAsync()
    {
        // Fade out
        _isPhraseTransitioning = true;
        StateHasChanged();

        // Wait for fade out animation to complete
        await Task.Delay(200);

        // Switch to the phrase for the selected length
        RecoveryPhrase = _phrasesByLength[_phraseLength];

        // Fade in
        _isPhraseTransitioning = false;
        StateHasChanged();
    }
    protected MudCarousel<object>? _carousel;
    protected bool IsLastSlide => _carousel != null && _carousel.SelectedIndex == _carousel.Items.Count - 1;
    protected List<string> RecoveryPhrase = [];
    private List<string> _importedPhraseWords = [];
    protected List<string> ImportedPhraseWords => _importedPhraseWords;

    private readonly Dictionary<int, List<string>> _phrasesByLength = new()
    {
        [12] = ["army", "bright", "coral", "dream", "elite", "frost", "grape", "hover", "ivory", "jewel", "karma", "lunar"],
        [15] = ["abandon", "bicycle", "captain", "dolphin", "eclipse", "fortune", "galaxy", "harmony", "impulse", "journey", "kitchen", "liberty", "machine", "nucleus", "october"],
        [24] = ["elephant", "quack", "taught", "light", "short", "hard", "help", "please", "earth", "cake", "control", "guard", "trust", "frost", "echo", "swim", "give", "seen", "eyes", "wade", "ice", "explain", "water", "geese"]
    };
    
    private bool _useFaceId = false;
    protected bool UseFaceId
    {
        get => _useFaceId;
        set
        {
            if (_useFaceId != value)
            {
                _useFaceId = value;
                StateHasChanged();
            }
        }
    }

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
        3 => "Next",
        4 => "Next",
        5 => "Continue",
        _ => "Next"
    };

    protected bool ShowSecondaryButton => CurrentSlide <= 2;

    protected static string GetStepLabel(int step) => step switch
    {
        1 => "Save Phrase",
        2 => "Verify",
        3 => "Name Wallet",
        4 => "Password",
        _ => string.Empty
    };

    protected override void OnInitialized()
    {
        AppStateService.CurrentSidebarContent = SidebarContentType.None;
        RecoveryPhrase = _phrasesByLength[_phraseLength];
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

    private void OnGoBackClicked()
    {
        _carousel?.Previous();
        if (_carousel != null)
        {
            CurrentSlide = _carousel.SelectedIndex;
        }
    }

    private async Task OnCopyPhraseClicked()
    {
        var phrase = string.Join(" ", RecoveryPhrase);
        await JsBridge.CopyToClipboardAsync(phrase);
        BurizaSnackbar.Show("Recovery phrase copied to clipboard", Severity.Success);
    }

    private async Task OnPastePhraseClicked()
    {
        try
        {
            var clipboardText = await JsBridge.ReadFromClipboardAsync();
            if (!string.IsNullOrWhiteSpace(clipboardText))
            {
                var words = clipboardText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                _importedPhraseWords = words.ToList();
                StateHasChanged();
            }
        }
        catch
        {
            BurizaSnackbar.Show("Unable to read from clipboard", Severity.Error);
        }
    }

    private void OnCreateNewWalletClicked()
    {
        // Go to slide 2 (Save This Phrase)
        _carousel?.MoveTo(1);
        if (_carousel != null)
        {
            CurrentSlide = _carousel.SelectedIndex;
        }
    }

    private void OnImportFromSeedClicked()
    {
        // Go to slide 3 (Recovery Phrase import)
        _carousel?.MoveTo(2);
        if (_carousel != null)
        {
            CurrentSlide = _carousel.SelectedIndex;
        }
    }

    private void OnFinishClicked()
    {
        Navigation.NavigateTo("/");
    }

    private async Task OnSecondaryButtonClicked() => await (CurrentSlide switch
    {
        0 => Task.Run(OnImportFromSeedClicked),
        1 => OnCopyPhraseClicked(),
        2 => OnPastePhraseClicked(),
        _ => Task.CompletedTask
    });

    private void OnPrimaryButtonClicked() => (CurrentSlide switch
    {
        0 => (Action)OnCreateNewWalletClicked,
        _ => OnNextButtonClicked
    })();
}