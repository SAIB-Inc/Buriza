using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Transaction;
using Buriza.Data.Models.Enums;
using Buriza.UI.Data.Dummy;
using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Pages;

public partial class History
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    [Inject]
    public required JavaScriptBridgeService JavaScriptBridgeService { get; set; }
    
    protected bool IsSummaryDisplayed { get; set; }
    protected TransactionHistory? SelectedTransaction { get; set; }
    protected Dictionary<string, List<TransactionHistory>> TransactionsByDate { get; set; } = new();
    
    protected override void OnInitialized()
    {
        AppStateService.CurrentSidebarContent = SidebarContentType.Portfolio;
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
}