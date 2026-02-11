using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Dialogs;

public partial class DisconnectDappDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    private void Cancel() => MudDialog.Cancel();

    private void Disconnect() => MudDialog.Close(DialogResult.Ok(true));
}
