using Buriza.UI.Resources;

namespace Buriza.UI.Components.Pages;

public partial class Send
{
    protected bool IsConfirmed { get; set; }
    
    public class TokenEntry
    {
        public string Name { get; set; } = "ADA";
        public string ImageSrc { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal Balance { get; set; } = 5304.01m;
    }
    
    public class Recipient
    {
        public int Id { get; set; }
        public string Address { get; set; } = "";
        public List<TokenEntry> TokenEntries { get; set; } = new() { new TokenEntry { ImageSrc = Tokens.Ada } };
    }
    
    private List<Recipient> recipients = new() { new Recipient { Id = 1 } };
    
    private void AddRecipient()
    {
        recipients.Add(new Recipient { Id = recipients.Count + 1 });
    }
    
    private void RemoveRecipient(Recipient recipient)
    {
        if (recipients.Count > 1)
        {
            recipients.Remove(recipient);
        }
    }
    
    private void AddToken(Recipient recipient)
    {
        recipient.TokenEntries.Add(new TokenEntry { ImageSrc = Tokens.Ada });
    }
    
    private void RemoveToken(Recipient recipient, TokenEntry entry)
    {
        if (recipient.TokenEntries.Count > 1)
        {
            recipient.TokenEntries.Remove(entry);
        }
    }
}