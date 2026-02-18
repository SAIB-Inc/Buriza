using Microsoft.AspNetCore.Components;
using MudBlazor;
using Buriza.UI.Services;

namespace Buriza.UI.Components.Dialogs;

public partial class ConfirmDataDialog
{
    [Inject]
    public required JavaScriptBridgeService JavaScriptBridgeService { get; set; }

    [CascadingParameter]
    private IMudDialogInstance DialogInstance { get; set; } = null!;

    private readonly string address = "test";

    private void Confirm()
    {
        DialogInstance.Close(DialogResult.Ok(true));
    }

    private void Cancel()
    {
        DialogInstance.Cancel();
    }

        private async Task CopyAddressToClipboard()
    {
        await JavaScriptBridgeService.CopyToClipboardAsync(address);
    }
}