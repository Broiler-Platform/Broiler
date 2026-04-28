using System.Drawing;

namespace Broiler.HTML.Image.Adapters;

internal interface ITextShaper
{
    SizeF MeasureString(FontAdapter font, string text);
    void MeasureString(FontAdapter font, string text, double maxWidth, out int charFit, out double charFitWidth);
    PointF GetDrawOrigin(FontAdapter font, PointF topLeft);
    float MeasureRenderedText(FontAdapter font, string text);
}
