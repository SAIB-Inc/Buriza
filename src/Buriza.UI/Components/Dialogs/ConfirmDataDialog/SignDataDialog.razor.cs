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

    protected bool IsSigningAddressExpanded { get; set; } = false;
    protected bool IsPayloadExpanded { get; set; } = false;

    // TODO: Replace with actual parsable state
    protected bool IsParsable { get; set; } = false;

    // TODO: Replace with actual data to confirm
    protected string Address { get; } = "addr1q9qvcgumeqdg8kjupqltvw0km5s4trle5ajswjzfyatqe25mwj0rva972882vkt0d8wcnfx0yhh3zpfr5q55ald3zmeqf3wyew";
    protected string HexPayload { get; } = "84AB00D901028282582003DE2AC5E9963B97888F726307B5FC99723170F87398C256AD04765796E8BC1600825820D3F281BCE2553995B5F955FC6FDD1AC97182256D270EF34630525CCA26B5E8C33A983A044534E454B1A7954R82080ABCDEF1234567890ABCD";

    protected void Confirm()
    {
        DialogInstance.Close(DialogResult.Ok(true));
    }

    protected void Cancel()
    {
        DialogInstance.Cancel();
    }

    protected async Task CopyAddressToClipboard()
    {
        await JavaScriptBridgeService.CopyToClipboardAsync(Address);
    }

    protected async Task CopyPayloadToClipboard()
    {
        await JavaScriptBridgeService.CopyToClipboardAsync(HexPayload);
    }

    protected void ExpandSigningAddress()
    {
        IsSigningAddressExpanded = !IsSigningAddressExpanded;
    }

    protected void ExpandPayload()
    {
        IsPayloadExpanded = !IsPayloadExpanded;
    }
}