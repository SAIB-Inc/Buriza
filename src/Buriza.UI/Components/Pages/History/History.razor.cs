using Buriza.UI.Services;
using Buriza.UI.Data.Dummy;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Pages;

public partial class History
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    [Inject]
    public required JavaScriptBridgeService JavaScriptBridgeService { get; set; }
    
    protected bool IsSummaryDisplayed { get; set; }
    protected bool IsSearchExpanded { get; set; }
    protected TransactionHistory? SelectedTransaction { get; set; }
    protected Dictionary<string, List<TransactionHistory>> TransactionsByDate { get; set; } = new();

    protected int CurrentPage { get; set; } = 1;
    protected int TotalPages { get; set; } = 4;

    protected void ToggleSearch()
    {
        IsSearchExpanded = !IsSearchExpanded;
    }
    
    protected override void OnInitialized()
    {
        AppStateService.CurrentSidebarContent = SidebarContentType.None;
        TransactionsByDate = TransactionHistoryData.GetTransactionsByDate();
    }

    protected async Task HandleTransactionClick(TransactionHistory transaction)
    {
        SelectedTransaction = transaction;
        int screenWidth = await JavaScriptBridgeService.GetScreenWidthAsync();

        if (screenWidth >= 1024)
        {
            AppStateService.IsFilterDrawerOpen = true;
        }
        else
        {
            IsSummaryDisplayed = true;
        }
    }
    
    protected string GetAmountDisplay(decimal amount, TransactionType type)
    {
        var prefix = type == TransactionType.Sent ? "-" : "+";
        return $"{prefix}{Math.Abs(amount):F2}";
    }

    protected void HandleFilterClick()
    {
        AppStateService.SetDrawerContent(DrawerContentType.Filter);
    }
}