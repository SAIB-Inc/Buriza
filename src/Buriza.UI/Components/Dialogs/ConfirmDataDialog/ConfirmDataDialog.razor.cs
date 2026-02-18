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

    // TODO: Replace with actual data to confirm
    private readonly string _address = "test";

    private bool _expanded = false;

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
        await JavaScriptBridgeService.CopyToClipboardAsync(_address);
    }

    private void ExpandCard()
    {
        _expanded = !_expanded;
    }
}