using Buriza.Core.Models.Transaction;
using Buriza.UI.Data.Dummy;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Microsoft.AspNetCore.Components;
using Buriza.Core.Models.Enums;

namespace Buriza.UI.Components.Layout;

public partial class HistorySidebarContent : ComponentBase
{
    protected Dictionary<string, List<TransactionHistory>> TransactionsByDate { get; set; } = new();

    protected override void OnInitialized()
    {
        TransactionsByDate = TransactionHistoryData.GetTransactionsByDate();
    }

    protected string GetBorderColorClass(TransactionType type) => type switch
    {
        TransactionType.Sent => "border-l-[var(--mud-palette-error)]",
        TransactionType.Received => "border-l-[var(--mud-palette-success)]",
        TransactionType.Mixed => "border-l-[var(--mud-palette-primary)]",
        _ => "border-l-[var(--mud-palette-text-secondary)]"
    };

    protected string GetAmountColorClass(decimal amount) =>
        amount >= 0 ? "!text-[var(--mud-palette-success)]" : "!text-[var(--mud-palette-error)]";

    protected string GetAmountDisplay(decimal amount, TransactionType type)
    {
        string prefix = type switch
        {
            TransactionType.Sent => "-",
            TransactionType.Received => "+",
            TransactionType.Mixed => amount >= 0 ? "+" : "-",
            _ => ""
        };
        return $"{prefix} ₳{Math.Abs(amount):F2}";
    }

    protected string TruncateHash(string hash)
    {
        if (string.IsNullOrEmpty(hash) || hash.Length <= 8)
            return hash;
        return $"{hash[..4]}...{hash[^4..]}";
    }
}
