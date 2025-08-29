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
    
    private List<TokenEntry> tokenEntries = new() { new TokenEntry { ImageSrc = Tokens.Ada } };
    
    private void AddToken()
    {
        tokenEntries.Add(new TokenEntry { ImageSrc = Tokens.Ada });
    }
}