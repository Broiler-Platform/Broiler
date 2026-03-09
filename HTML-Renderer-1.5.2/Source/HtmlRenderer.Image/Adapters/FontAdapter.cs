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

        // Calculate metrics
        var metrics = Font.Metrics;
        _height = metrics.Descent - metrics.Ascent;
        _underlineOffset = -metrics.Ascent + metrics.UnderlinePosition.GetValueOrDefault(metrics.Descent - metrics.Ascent * 0.87f);
    }

    public SKFont Font { get; }
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
