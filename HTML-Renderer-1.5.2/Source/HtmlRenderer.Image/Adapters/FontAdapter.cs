using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.Image.Adapters;

internal sealed class FontAdapter : RFont
{
    private readonly double _size;
    private readonly FontStyle _style;
    private double _height = -1;
    private double _underlineOffset = -1;
    private double _whitespaceWidth = -1;

    public FontAdapter(SKTypeface typeface, double size, FontStyle style)
    {
        Typeface = typeface;
        _size = size;
        _style = style;
        // Phase 10.2: Use grayscale anti-aliasing (Antialias) instead of
        // SubpixelAntialias.  The Chromium reference screenshot is a bitmap
        // where sub-pixel colour fringes have been composited away, so
        // grayscale AA produces glyph shapes that match the reference more
        // closely and eliminates per-sub-pixel colour differences.
        // Priority 2: Enable sub-pixel text positioning (Subpixel = true)
        // for more precise glyph placement.  This is orthogonal to the AA
        // edging mode and aligns baseline positioning with Chromium's
        // HarfBuzz/FreeType stack.
        Font = new SKFont(typeface, (float)size)
        {
            Edging = SKFontEdging.Antialias,
            Subpixel = true
        };

        // Priority 1 – Font size coordinate fix: The internal coordinate
        // system uses typographic points (size passed here is in pt), but
        // SkiaSharp's SKFont.Size is interpreted as canvas pixels.  Create
        // a second font at CSS px size (×96/72) for glyph rendering so
        // that characters are drawn at the correct CSS pixel dimensions.
        // The layout font (Font) is kept at pt size so that all existing
        // metrics, text measurement, and layout calculations are preserved.
        RenderFont = new SKFont(typeface, (float)(size * (96.0 / 72.0)))
        {
            Edging = SKFontEdging.Antialias,
            Subpixel = true
        };

        // Calculate metrics from the layout font (pt-based)
        var metrics = Font.Metrics;
        _height = metrics.Descent - metrics.Ascent;
        _underlineOffset = -metrics.Ascent + metrics.UnderlinePosition.GetValueOrDefault(metrics.Descent - metrics.Ascent * 0.87f);
    }

    /// <summary>Layout font (pt-based) – used for metrics and text measurement.</summary>
    public SKFont Font { get; }

    /// <summary>Render font (CSS px-based) – used for drawing glyphs at correct size.</summary>
    public SKFont RenderFont { get; }

    public SKTypeface Typeface { get; }

    public override double Size => _size;
    public override double Height => _height;
    public override double UnderlineOffset => _underlineOffset;
    public override double LeftPadding => _height / 6.0;

    public override double GetWhitespaceWidth(RGraphics graphics)
    {
        if (_whitespaceWidth < 0)
            _whitespaceWidth = graphics.MeasureString(" ", this).Width;

        return _whitespaceWidth;
    }

    internal void SetMetrics(double height, double underlineOffset)
    {
        _height = height;
        _underlineOffset = underlineOffset;
    }
}
