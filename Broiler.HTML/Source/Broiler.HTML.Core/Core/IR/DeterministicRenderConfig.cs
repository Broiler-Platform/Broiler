namespace TheArtOfDev.HtmlRenderer.Core.IR;

/// <summary>
/// Configuration for deterministic pixel-regression rendering (Phase 5).
/// All settings are chosen to eliminate cross-platform and cross-run variation.
/// </summary>
public sealed record DeterministicRenderConfig
{
    /// <summary>Viewport width in pixels.</summary>
    public int ViewportWidth { get; init; } = 800;

    /// <summary>Viewport height in pixels.</summary>
    public int ViewportHeight { get; init; } = 600;

    /// <summary>Pixel-difference threshold as a ratio (0.0–1.0). Default 0.001 = 0.1%.</summary>
    public double PixelDiffThreshold { get; init; } = 0.001;

    /// <summary>Per-channel colour tolerance for fuzzy pixel matching (0–255).</summary>
    public int ColorTolerance { get; init; } = 5;

    /// <summary>
    /// Returns the default configuration (800×600, 0.1% threshold, 5-channel tolerance).
    /// </summary>
    public static DeterministicRenderConfig Default { get; } = new();
}
