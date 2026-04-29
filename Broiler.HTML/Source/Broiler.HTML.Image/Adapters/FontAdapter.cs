using System;
using System.Drawing;
using Broiler.HTML.Adapters.Adapters;
using SkiaSharp;
using SixLabors.Fonts;
using DrawingFontStyle = System.Drawing.FontStyle;
using SixLaborsFont = SixLabors.Fonts.Font;

namespace Broiler.HTML.Image.Adapters;

internal sealed class FontAdapter : RFont
{
    /// <summary>
    /// Ratio to convert typographic points to CSS pixels (96 DPI / 72 DPI).
    /// </summary>
    private const double PtToCssPx = 96.0 / 72.0;
    private readonly double _size;
    private readonly string _family;
    private readonly DrawingFontStyle _style;
    private readonly Func<SKTypeface>? _compatTypefaceFactory;
    private double _height = -1;
    private double _underlineOffset = -1;
    private double _whitespaceWidth = -1;
    private SixLaborsFont? _broilerLayoutFont;
    private SixLaborsFont? _broilerRenderFont;
    private SKTypeface? _typeface;
    private SKFont? _font;
    private SKFont? _renderFont;

    public FontAdapter(SKTypeface typeface, double size, DrawingFontStyle style)
        : this((typeface ?? SKTypeface.Default).FamilyName, size, style, () => typeface ?? SKTypeface.Default)
    {
    }

    public FontAdapter(string family, double size, DrawingFontStyle style, Func<SKTypeface>? compatTypefaceFactory = null)
    {
        _family = family;
        _size = size;
        _style = style;
        _compatTypefaceFactory = compatTypefaceFactory;
    }

    /// <summary>Layout font (pt-based) – used for metrics and text measurement.</summary>
    public SKFont Font => _font ??= CreateConfiguredFont((float)_size);

    /// <summary>Render font (CSS px-based) – used for drawing glyphs at correct size.</summary>
    public SKFont RenderFont => _renderFont ??= CreateConfiguredFont((float)(_size * PtToCssPx));

    public SKTypeface Typeface => _typeface ??= _compatTypefaceFactory?.Invoke() ?? SKTypeface.Default;

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

    internal bool HasMaterializedLayoutFont => _broilerLayoutFont is not null || _font is not null;

    internal bool HasMaterializedRenderFont => _broilerRenderFont is not null || _renderFont is not null;

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

        if (TryGetBroilerLayoutFont(out var broilerFont))
        {
            var broilerMetrics = broilerFont.FontMetrics;
            var vertical = broilerMetrics.VerticalMetrics;
            float scale = broilerFont.Size / broilerMetrics.UnitsPerEm;
            _height = vertical.LineHeight * scale;
            _underlineOffset = (vertical.Ascender - broilerMetrics.UnderlinePosition) * scale;
            return;
        }

        var skiaMetrics = Font.Metrics;
        _height = skiaMetrics.Descent - skiaMetrics.Ascent;
        _underlineOffset = -skiaMetrics.Ascent + skiaMetrics.UnderlinePosition.GetValueOrDefault(skiaMetrics.Descent - skiaMetrics.Ascent * 0.87f);
    }

    internal bool TryGetBroilerLayoutFont(out SixLaborsFont font)
    {
        if (_broilerLayoutFont is not null)
        {
            font = _broilerLayoutFont;
            return true;
        }

        if (BroilerFontRegistry.TryCreateFont(_family, (float)_size, _style, out font))
        {
            _broilerLayoutFont = font;
            return true;
        }

        return false;
    }

    internal bool TryGetBroilerRenderFont(out SixLaborsFont font)
    {
        if (_broilerRenderFont is not null)
        {
            font = _broilerRenderFont;
            return true;
        }

        if (BroilerFontRegistry.TryCreateFont(_family, (float)_size, _style, out font))
        {
            _broilerRenderFont = font;
            return true;
        }

        return false;
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
