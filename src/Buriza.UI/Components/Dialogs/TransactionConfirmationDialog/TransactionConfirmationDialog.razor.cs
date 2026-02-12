using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Dialogs;

public partial class TransactionConfirmationDialog
{
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;

    private void Cancel() => MudDialog.Cancel();

    private void Disconnect() => MudDialog.Close(DialogResult.Ok(true));
}