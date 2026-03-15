namespace TheArtOfDev.HtmlRenderer.Core.IR;

/// <summary>
/// Draws a <see cref="DisplayList"/> to a platform surface.
/// Implementations: SkiaRasterBackend, WpfRasterBackend.
/// </summary>
/// <remarks>
/// Phase 1: Interface definition only. No implementations yet.
/// </remarks>
public interface IRasterBackend
{
    /// <summary>
    /// Replays the display list items onto the given surface.
    /// </summary>
    /// <param name="list">The display list to render.</param>
    /// <param name="surface">Platform-specific surface (e.g. SKSurface, DrawingContext).</param>
    void Render(DisplayList list, object surface);
}
