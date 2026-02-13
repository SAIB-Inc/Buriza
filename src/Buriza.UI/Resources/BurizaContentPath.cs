namespace Buriza.UI.Resources;

public static class BurizaContentPath
{
    public static string Root { get; set; } = "_content";

    public static string Resolve(string path) => $"{Root}/{path}";
}
