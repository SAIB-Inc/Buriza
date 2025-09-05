namespace Buriza.Data.Models.Common;

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
    public List<TokenEntry> TokenEntries { get; set; } = [];
}