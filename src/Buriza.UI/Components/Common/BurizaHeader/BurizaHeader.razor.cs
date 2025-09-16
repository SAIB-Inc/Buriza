using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Common;

public partial class BurizaHeader
{
    [Inject]
    public required NavigationManager Navigation { get; set; }
    
    protected bool IsPopupVisible = false;
    
    protected void TogglePopup()
    {
        IsPopupVisible = !IsPopupVisible;
    }

    protected void ClosePopup()
    {
        IsPopupVisible = false;
    }
    
    protected string GetSpanClass(string route)
    {
        bool isActive = Navigation.Uri.Contains(route) || (route == "/" && (Navigation.Uri.EndsWith("/") || Navigation.Uri.Contains("/popup") || Navigation.Uri.Contains("/options") || Navigation.Uri.Contains("/index")));
        return isActive 
            ? "text-[11px] !text-[var(--mud-palette-primary-text)] !bg-[var(--mud-palette-primary-lighten)] !rounded-full !px-1 !py-[2px]"
            : "text-[11px] !text-[var(--mud-palette-text-secondary)] !px-1 !py-[2px]";
    }
}