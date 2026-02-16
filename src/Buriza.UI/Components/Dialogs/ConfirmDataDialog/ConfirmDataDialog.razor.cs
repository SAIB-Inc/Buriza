using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Dialogs;

public partial class ConfirmDataDialog
{
    [CascadingParameter]
    private IMudDialogInstance DialogInstance { get; set; } = null!;

    private void Confirm()
    {
        DialogInstance.Close(DialogResult.Ok(true));
    }

    private void Cancel()
    {
        DialogInstance.Cancel();
    }
}