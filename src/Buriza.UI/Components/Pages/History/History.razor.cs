namespace Buriza.UI.Components.Pages;

public partial class History
{
    protected bool IsSummaryDisplayed { get; set; }
    
    private string GetRelativeTime(DateTime timestamp)
    {
        TimeSpan diff = DateTime.Now - timestamp;
        
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} minutes ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} hours ago";
        if (diff.TotalDays < 30)
            return $"{(int)diff.TotalDays} days ago";
        
        return timestamp.ToString("MMM dd, yyyy");
    }
}