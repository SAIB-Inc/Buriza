using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;

namespace Buriza.UI.Components.Common;

public partial class BurizaNavLink
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    [Parameter]
    public string Icon { get; set; } = string.Empty;

    [Parameter]
    public string? Href { get; set; }

    [Parameter]
    public string Class { get; set; } = string.Empty;

    [Parameter]
    public NavLinkMatch Match { get; set; } = NavLinkMatch.All;

    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private const string BaseClass = "[&_.mud-icon-root]:!text-[var(--mud-palette-text-primary)] [&_.mud-nav-link-text]:transition-opacity [&_.mud-nav-link-text]:duration-300 [&_.mud-nav-link-text]:whitespace-nowrap";

    private const string ActiveClass = "[&_.mud-icon-root]:text-[var(--mud-palette-primary-text)]! !text-[var(--mud-palette-primary-text)] !bg-[var(--mud-palette-primary-lighten)]";

    private string NavLinkClass => $"{BaseClass} [&_.mud-nav-link-text]:{(AppStateService.IsSidebarOpen ? "opacity-100" : "opacity-0")} {Class}";
}
