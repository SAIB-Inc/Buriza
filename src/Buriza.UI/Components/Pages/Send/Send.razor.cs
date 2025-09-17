using Buriza.UI.Resources;
using Microsoft.AspNetCore.Components;
using Buriza.Data.Models.Common;
using Buriza.UI.Services;

namespace Buriza.UI.Components.Pages;

public partial class Send
{
    [Inject]
    public required NavigationManager Navigation { get; set; } = null!;

    [Inject]
    public required AppStateService AppStateService { get; set; }

    [Inject]
    public required JavaScriptBridgeService JavaScriptBridgeService { get; set; }
    
    protected bool IsConfirmed { get; set; }
    protected bool IsQrScan { get; set; }
    protected bool ShowOverlay { get; set; }
    
    protected List<Recipient> recipients = new() { new Recipient { Id = 1, TokenEntries = [new TokenEntry { ImageSrc = Tokens.Ada }] } };
    
    protected void AddRecipient()
    {
        recipients.Add(new Recipient { Id = recipients.Count + 1, TokenEntries = [new TokenEntry { ImageSrc = Tokens.Ada }] });
    }
    
    protected void RemoveRecipient(Recipient recipient)
    {
        if (recipients.Count > 1)
        {
            recipients.Remove(recipient);
        }
    }
    
    protected void AddToken(Recipient recipient)
    {
        recipient.TokenEntries.Add(new TokenEntry { ImageSrc = Tokens.Ada });
    }
    
    protected void RemoveToken(Recipient recipient, TokenEntry entry)
    {
        if (recipient.TokenEntries.Count > 1)
        {
            recipient.TokenEntries.Remove(entry);
        }
    }
    
    protected async Task HandleSend()
    {
        if (IsConfirmed)
        {
            int screenWidth = await JavaScriptBridgeService.GetScreenWidthAsync();
            if (screenWidth <= 376)
            {
                Navigation.NavigateTo("/transaction/success");
            }
            else
            {
                ShowOverlay = true;
            }
        }
        else
        {
            IsConfirmed = true;
        }
    }

    protected void HandleOverlayClick()
    {
        ShowOverlay = false;
        Navigation.NavigateTo("/transaction/success");
    }
}