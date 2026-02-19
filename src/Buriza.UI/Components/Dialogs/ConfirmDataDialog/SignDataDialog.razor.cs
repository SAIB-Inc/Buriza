using Microsoft.AspNetCore.Components;
using MudBlazor;
using Buriza.UI.Services;

namespace Buriza.UI.Components.Dialogs;

public partial class SignDataDialog
{
    [Inject]
    public required JavaScriptBridgeService JavaScriptBridgeService { get; set; }

    [CascadingParameter]
    private IMudDialogInstance DialogInstance { get; set; } = null!;

    private bool _IsSigningAddressExpanded = false;
    private bool _IsPayloadExpanded = false;

    // TODO: Replace with actual parsable state
    private bool _IsParsable = true;

    // TODO: Replace with actual data to confirm
    private readonly string _address = "addr1q9qvcgumeqdg8kjupqltvw0km5s4trle5ajswjzfyatqe25mwj0rva972882vkt0d8wcnfx0yhh3zpfr5q55ald3zmeqf3wyew";
    private readonly string _hexPayload = "84AB00D901028282582003DE2AC5E9963B97888F726307B5FC99723170F87398C256AD04765796E8BC1600825820D3F281BCE2553995B5F955FC6FDD1AC97182256D270EF34630525CCA26B5E8C33A983A044534E454B1A7954R82080ABCDEF1234567890ABCD";

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

    private async Task CopyPayloadToClipboard()
    {
        await JavaScriptBridgeService.CopyToClipboardAsync(_hexPayload);
    }

    private void ExpandSigningAddress()
    {
        _IsSigningAddressExpanded = !_IsSigningAddressExpanded;
    }

    private void ExpandPayload()
    {
        _IsPayloadExpanded = !_IsPayloadExpanded;
    }
}