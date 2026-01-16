using Buriza.Data.Models.Enums;

namespace Buriza.UI;

public static class Utils
{
    public static string GetRelativeTime(DateTime timestamp)
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

    public static string GetTransactionTypeText(TransactionType type)
    {
        return type == TransactionType.Sent ? "Sent:" : "Received:";
    }

    public static string GetTransactionTypeColor(TransactionType type)
    {
        return type == TransactionType.Sent ? "border-[var(--mud-palette-error)]!" : "border-[var(--mud-palette-success)]!";
    }
}
