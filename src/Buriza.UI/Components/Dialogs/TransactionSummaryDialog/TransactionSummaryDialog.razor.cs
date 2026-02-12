using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Dialogs;

public partial class TransactionSummaryDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    protected bool IsSingleAsset { get; set; }
    protected bool IsAssetsExpanded { get; set; } = true;
    protected bool IsMintInfoExpanded { get; set; }
    protected bool IsRewardsExpanded { get; set; }
    protected bool IsAdditionalInfoExpanded { get; set; }
    protected bool IsRawDataExpanded { get; set; }

    private void Cancel() => MudDialog.Cancel();

    private void Confirm() => MudDialog.Close(DialogResult.Ok(true));
}
