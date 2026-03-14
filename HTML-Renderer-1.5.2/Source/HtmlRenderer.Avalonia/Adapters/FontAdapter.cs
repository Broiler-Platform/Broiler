using System;
using Avalonia;
using Avalonia.Media;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.Avalonia.Adapters;

internal sealed class FontAdapter : RFont
{
    private readonly double _size;
    private readonly double _underlineOffset;
    private readonly double _height;
    private double _whitespaceWidth = -1;

    public FontAdapter(Typeface typeface, double size)
    {
        Typeface = typeface;
        _size = size;

        // Avalonia uses device-independent pixels (96 DPI).
        // Font size is specified in points; convert pt → px: px = pt * 96/72.
        double emPx = 96d / 72d * _size;

        // Use font metrics from the typeface to compute line height and underline offset.
        // LineHeight ≈ emSize * (ascent + descent + lineGap) / emSize ≈ lineSpacing factor.
        // Avalonia's GlyphTypeface provides these metrics normalised to 1 em.
        if (typeface.GlyphTypeface is { } glyphTypeface)
        {
            double ascent = glyphTypeface.Metrics.Ascent;
            double descent = Math.Abs(glyphTypeface.Metrics.Descent);
            double lineGap = glyphTypeface.Metrics.LineGap;

            _height = emPx * (ascent + descent + lineGap);
            _underlineOffset = emPx * (ascent + glyphTypeface.Metrics.UnderlinePosition);

            GlyphTypeface = glyphTypeface;
        }
        else
        {
            // Fallback: approximate from font family line spacing.
            _height = emPx * 1.2;
            _underlineOffset = emPx * 1.0;
        }
    }

    public Typeface Typeface { get; }

    public IGlyphTypeface GlyphTypeface { get; }

    public override double Size => _size;

    public override double UnderlineOffset => _underlineOffset;

    public override double Height => _height;

    public override double LeftPadding => _height / 6f;

    public override double GetWhitespaceWidth(RGraphics graphics)
    {
        if (_whitespaceWidth < 0)
            _whitespaceWidth = graphics.MeasureString(" ", this).Width;

        return _whitespaceWidth;
    }
}
