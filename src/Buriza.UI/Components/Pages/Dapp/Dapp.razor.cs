using Buriza.UI.Services;
using Buriza.UI.Data.Dummy;
using Buriza.UI.Components.Dialogs;
using Buriza.Data.Models.Enums;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Pages;

public partial class Dapp
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    [Inject]
    public required JavaScriptBridgeService JavaScriptBridgeService { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    protected int ExpandedCard = 1;
    protected int ExpandedDesktopSlide = 0;
    protected bool ShowAuthorization = false;
    protected string SelectedCategory = "All";
    protected int CurrentPage = 1;
    protected int PageSize = 10;
    protected int MaxConnectedAppsDisplay = 6;
    protected int TotalPages => (int)Math.Ceiling((double)Dapps.Count / PageSize);
    protected List<DappInfo> PagedDapps => Dapps.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
    protected List<DappInfo> Dapps { get; set; } = [];
    protected List<DappInfo> ConnectedApps { get; set; } = [];
    protected List<DappInfo> VisibleConnectedApps => ConnectedApps.Take(MaxConnectedAppsDisplay).ToList();
    protected bool HasMoreConnectedApps => ConnectedApps.Count > MaxConnectedAppsDisplay;
    protected List<string> Categories { get; set; } = [];

    protected override void OnInitialized()
    {
        AppStateService.CurrentSidebarContent = SidebarContentType.None;
        Categories = DappData.GetCategories();
        Dapps = DappData.GetAllDapps();
        ConnectedApps = DappData.GetAllDapps();
    }

    protected void OnCategoryChanged(string category)
    {
        SelectedCategory = category;
        Dapps = DappData.GetDappsByCategory(category);
    }

    protected void ExpandCard(int cardIndex)
    {
        ExpandedCard = cardIndex;
    }
    
    protected void ExpandDesktopSlide(int slideIndex)
    {
        ExpandedDesktopSlide = slideIndex;
    }
    
    protected async Task OnDappCardClicked()
    {
        int screenWidth = await JavaScriptBridgeService.GetScreenWidthAsync();

        if (screenWidth >= 1024)
        {
            AppStateService.SetDrawerContent(DrawerContentType.AuthorizeDapp);
        }
        else
        {
            ShowAuthorization = true;
        }
    }

    protected void OnSeeAllConnectedAppsClicked()
    {
        AppStateService.SetDrawerContent(DrawerContentType.ConnectedApps);
    }

    protected async Task OnDisconnectDappClicked(DappInfo dapp)
    {
        var options = new DialogOptions
        {
            NoHeader = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
            BackdropClick = true
        };

        var dialog = await DialogService.ShowAsync<DisconnectDappDialog>(null, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            ConnectedApps.Remove(dapp);
            StateHasChanged();
        }
    }

    protected string GetDappTypeColor(string type) => type.ToLower() switch
    {
        "defi" => "var(--dapp-type-defi)",
        "nft" => "var(--dapp-type-nft)",
        "gaming" => "var(--dapp-type-gaming)",
        "governance" => "var(--dapp-type-governance)",
        "social" => "var(--dapp-type-social)",
        "bridge" => "var(--dapp-type-bridge)",
        "infrastructure" => "var(--dapp-type-infrastructure)",
        "realfi" => "var(--dapp-type-realfi)",
        _ => "var(--mud-palette-text-secondary)"
    };
}