using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Pages;

public partial class Settings
{
    [Inject]
    public required AppStateService AppStateService { get; set; }
}