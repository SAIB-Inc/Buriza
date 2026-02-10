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
        AppStateService.SummaryTransactionType = transaction.Type;
        AppStateService.SummaryTransactionCategory = transaction.Category;
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
        var prefix = type switch
        {
            TransactionType.Sent => "-",
            TransactionType.Received => "+",
            TransactionType.Mixed => amount >= 0 ? "+" : "-",
            _ => ""
        };
        return $"{prefix} ₳{Math.Abs(amount):F2}";
    }

    protected string GetBorderColorClass(TransactionType type) => type switch
    {
        TransactionType.Sent => "border-l-[var(--mud-palette-error)]",
        TransactionType.Received => "border-l-[var(--mud-palette-success)]",
        TransactionType.Mixed => "border-l-[var(--mud-palette-primary)]",
        _ => "border-l-[var(--mud-palette-text-secondary)]"
    };

    protected string GetTextColorClass(TransactionType type) => type switch
    {
        TransactionType.Sent => "!text-[var(--mud-palette-error)]",
        TransactionType.Received => "!text-[var(--mud-palette-success)]",
        TransactionType.Mixed => "!text-[var(--mud-palette-primary)]",
        _ => "!text-[var(--mud-palette-text-secondary)]"
    };

    protected string GetAmountColorClass(decimal amount) =>
        amount >= 0 ? "!text-[var(--mud-palette-success)]" : "!text-[var(--mud-palette-error)]";

    protected string GetStatusLabel(TransactionType type) => type switch
    {
        TransactionType.Sent => "Sent",
        TransactionType.Received => "Received",
        TransactionType.Mixed => "Mixed",
        _ => "Unknown"
    };

    protected static string GetCategoryLabel(TransactionCategory category) => category switch
    {
        TransactionCategory.Contract => "Contract",
        TransactionCategory.Mint => "Mint",
        TransactionCategory.Delegation => "Delegation",
        TransactionCategory.Withdrawal => "Withdrawal",
        TransactionCategory.Governance => "Governance",
        _ => ""
    };

    protected static string GetCategoryChipClass(TransactionCategory category) => category switch
    {
        TransactionCategory.Contract => "!bg-[var(--mud-palette-secondary-lighten)]",
        TransactionCategory.Mint => "!bg-[var(--mud-palette-secondary-darken)]",
        _ => "!bg-[var(--mud-palette-secondary)]"
    };

    protected void HandleFilterClick()
    {
        AppStateService.SetDrawerContent(DrawerContentType.Filter);
    }
}