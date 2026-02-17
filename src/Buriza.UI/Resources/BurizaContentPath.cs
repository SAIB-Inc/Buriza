namespace Buriza.UI.Resources;

/// <summary>
/// Resolves static asset paths for the Buriza.UI Razor Class Library.
/// Defaults to "_content" which is the standard RCL convention for Blazor WASM and MAUI Hybrid.
/// Set to "content" for browser extensions (Chrome prohibits underscore-prefixed folders).
/// Must be set once at application startup before any components render.
/// </summary>
public static class BurizaContentPath
{
    public static string Root { get; set; } = "_content";

    public static string Resolve(string path) => $"{Root}/Buriza.UI/{path}";
}
