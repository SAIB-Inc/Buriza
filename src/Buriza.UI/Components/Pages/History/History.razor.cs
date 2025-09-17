using Buriza.UI.Services;
using Buriza.UI.Data;
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
    protected TransactionHistory? SelectedTransaction { get; set; }
    protected Dictionary<string, List<TransactionHistory>> TransactionsByDate { get; set; } = new();
    
    protected override void OnInitialized()
    {
        TransactionsByDate = TransactionHistoryData.GetTransactionsByDate();
    }

    protected string GetRelativeTime(DateTime timestamp)
    {
        TimeSpan diff = DateTime.Now - timestamp;

        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} minutes ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} hours ago";
        if (diff.TotalDays < 30)
            return $"{(int)diff.TotalDays} days ago";

        return timestamp.ToString("MMM dd, yyyy");
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
    
    protected string GetTransactionTypeText(TransactionType type)
    {
        return type == TransactionType.Sent ? "Sent:" : "Received:";
    }
    
    protected string GetTransactionTypeColor(TransactionType type)
    {
        return type == TransactionType.Sent ? "var(--mud-palette-error)" : "var(--mud-palette-success)";
    }
    
    protected string GetAmountDisplay(decimal amount, TransactionType type)
    {
        var prefix = type == TransactionType.Sent ? "-" : "+";
        return $"{prefix}{Math.Abs(amount):F2}";
    }
}