using MudBlazor;

namespace Buriza.UI.Components.Pages;

public partial class OnBoard
{
    protected int PhraseLength { get; set; } = 24;
    protected MudCarousel<object>? _carousel;
    protected bool IsLastSlide => _carousel != null && _carousel.SelectedIndex == _carousel.Items.Count - 1;
    protected List<string> RecoveryPhrase = ["random", "quack", "taught", "light", "short", "hard", "help", "please", "earth", "cake", "control", "guard", "trust", "frost", "echo", "swim", "give", "seen", "eyes", "wade", "ice", "explain", "water", "geese"];

    private void OnNextButtonClicked()
    {
        _carousel?.Next();
    }
}