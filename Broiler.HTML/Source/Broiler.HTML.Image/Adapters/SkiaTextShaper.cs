using System.Drawing;

namespace Broiler.HTML.Image.Adapters;

internal sealed class SkiaTextShaper : ITextShaper
{
    private SkiaTextShaper() { }

    public static SkiaTextShaper Instance { get; } = new();

    public SizeF MeasureString(FontAdapter font, string text)
    {
        var width = font.Font.MeasureText(text);
        return new SizeF(width, (float)font.Height);
    }

    public void MeasureString(FontAdapter font, string text, double maxWidth, out int charFit, out double charFitWidth)
    {
        charFit = 0;
        charFitWidth = 0;

        for (int i = 1; i <= text.Length; i++)
        {
            var substring = text.Substring(0, i);
            var width = font.Font.MeasureText(substring);
            if (width > maxWidth)
                break;

            charFit = i;
            charFitWidth = width;
        }
    }

    public PointF GetDrawOrigin(FontAdapter font, PointF topLeft)
    {
        var metrics = font.RenderFont.Metrics;
        return new PointF(topLeft.X, topLeft.Y - metrics.Ascent);
    }

    public float MeasureRenderedText(FontAdapter font, string text) =>
        font.RenderFont.MeasureText(text);
}
