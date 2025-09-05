using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Pages;

public partial class SelectAsset
{
    [Inject]
    public required AppStateService AppStateService { get; set; }
}