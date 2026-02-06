using Buriza.Core.Models.Transaction;
using Buriza.UI.Data.Dummy;
using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Layout;

public partial class HistorySidebarContent : ComponentBase
{
    protected Dictionary<string, List<TransactionHistory>> TransactionsByDate { get; set; } = new();

    protected override void OnInitialized()
    {
        TransactionsByDate = TransactionHistoryData.GetTransactionsByDate();
    }
}
