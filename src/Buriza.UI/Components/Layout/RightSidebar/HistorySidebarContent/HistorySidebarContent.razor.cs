using Buriza.UI.Data.Dummy;
using Buriza.Data.Models.Common;
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
