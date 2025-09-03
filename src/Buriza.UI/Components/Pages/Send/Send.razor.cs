using Buriza.UI.Resources;
using Microsoft.AspNetCore.Components;
using Buriza.Data.Models.Common;

namespace Buriza.UI.Components.Pages;

public partial class Send
{
    [Inject]
    public required NavigationManager Navigation { get; set; } = null!;
    
    protected bool IsConfirmed { get; set; }
    
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
    
    protected void HandleSend()
    {
        if (IsConfirmed)
        {
            Navigation.NavigateTo("/transaction/success");
        }
        else
        {
            IsConfirmed = true;
        }
    }
}