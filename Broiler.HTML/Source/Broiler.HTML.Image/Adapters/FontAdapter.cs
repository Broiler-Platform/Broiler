using System.Drawing;
using Broiler.HTML.Adapters.Adapters;
using SkiaSharp;

namespace Broiler.HTML.Image.Adapters;

internal sealed class FontAdapter : RFont
{
    /// <summary>
    /// Ratio to convert typographic points to CSS pixels (96 DPI / 72 DPI).
    /// </summary>
    private const double PtToCssPx = 96.0 / 72.0;
    private readonly double _size;
    private double _height = -1;
    private double _underlineOffset = -1;
    private double _whitespaceWidth = -1;
    private SKFont? _font;
    private SKFont? _renderFont;

    public FontAdapter(SKTypeface typeface, double size, FontStyle style)
    {
        Typeface = typeface;
        _size = size;
    }

    /// <summary>Layout font (pt-based) – used for metrics and text measurement.</summary>
    public SKFont Font => _font ??= CreateConfiguredFont((float)_size);

    /// <summary>Render font (CSS px-based) – used for drawing glyphs at correct size.</summary>
    public SKFont RenderFont => _renderFont ??= CreateConfiguredFont((float)(_size * PtToCssPx));

    public SKTypeface Typeface { get; }

    public override double Size => _size;
    public override double Height
    {
        get
        {
            EnsureMetrics();
            return _height;
        }
    }

    public override double UnderlineOffset
    {
        get
        {
            EnsureMetrics();
            return _underlineOffset;
        }
    }

    public override double LeftPadding => Height / 6.0;

    internal bool HasMaterializedLayoutFont => _font is not null;

    internal bool HasMaterializedRenderFont => _renderFont is not null;

    public override double GetWhitespaceWidth(RGraphics graphics)
    {
        if (_whitespaceWidth < 0)
            _whitespaceWidth = graphics.MeasureString(" ", this).Width;

        return _whitespaceWidth;
    }

    private void EnsureMetrics()
    {
        if (_height >= 0 && _underlineOffset >= 0)
            return;

        var metrics = Font.Metrics;
        _height = metrics.Descent - metrics.Ascent;
        _underlineOffset = -metrics.Ascent + metrics.UnderlinePosition.GetValueOrDefault(metrics.Descent - metrics.Ascent * 0.87f);
    }

    private SKFont CreateConfiguredFont(float size) =>
        new(Typeface, size)
        {
            // Phase 10.2: Use grayscale anti-aliasing (Antialias) instead of
            // SubpixelAntialias. The Chromium reference screenshot is a bitmap
            // where sub-pixel colour fringes have been composited away, so
            // grayscale AA produces glyph shapes that match the reference more
            // closely and eliminates per-sub-pixel colour differences.
            // Priority 2: Enable sub-pixel text positioning (Subpixel = true)
            // for more precise glyph placement. This is orthogonal to the AA
            // edging mode and aligns baseline positioning with Chromium's
            // HarfBuzz/FreeType stack.
            Edging = SKFontEdging.Antialias,
            Subpixel = true
        };
}
