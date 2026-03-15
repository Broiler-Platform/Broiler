namespace TheArtOfDev.HtmlRenderer.Core.IR;

/// <summary>
/// Classifies the root cause of a pixel-regression failure (Phase 5).
/// Used only when a pixel test has already failed.
/// </summary>
public enum FailureClassification
{
    /// <summary>Fragment tree changed → layout regression.</summary>
    LayoutDiff,

    /// <summary>Fragment tree unchanged but DisplayList changed → paint regression.</summary>
    PaintDiff,

    /// <summary>Both Fragment tree and DisplayList unchanged → pure raster regression.</summary>
    RasterDiff
}
