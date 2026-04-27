namespace Broiler.HTML.Image;

/// <summary>
/// Identifies the active graphics backend behind the Broiler-owned image
/// abstractions during the Skia replacement migration.
/// </summary>
public static class BGraphicsBackend
{
    /// <summary>
    /// Stable machine-readable identifier for the current backend.
    /// </summary>
    public static string CurrentId => "skia";

    /// <summary>
    /// Human-readable name for the current backend implementation.
    /// </summary>
    public static string CurrentDisplayName => "SkiaSharp";

    /// <summary>
    /// Human-readable combined backend label for diagnostics and artifacts.
    /// </summary>
    public static string CurrentLabel => $"{CurrentId} ({CurrentDisplayName})";
}
