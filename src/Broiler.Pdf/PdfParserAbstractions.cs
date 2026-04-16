namespace Broiler.Pdf;

internal interface IPdfDocumentParser
{
    IPdfDocument Open(string path);
}

internal interface IPdfDocument : IDisposable
{
    IReadOnlyList<IPdfPage> Pages { get; }
}

internal interface IPdfPage
{
    int Number { get; }
    string Text { get; }
    PdfRectangle MediaBox { get; }
    PdfPageLayout ExtractLayout();
}

internal readonly record struct PdfRectangle(double Left, double Bottom, double Right, double Top)
{
    public double Width => Math.Max(0, Right - Left);

    public double Height => Math.Max(0, Top - Bottom);
}

internal sealed record PdfPageLayout(
    IReadOnlyList<PdfPositionedText> Text,
    IReadOnlyList<PdfPositionedImage> Images);

internal sealed record PdfPositionedText(
    string Text,
    double Left,
    double Top,
    double FontSize,
    string? FontName,
    bool IsBold,
    bool IsItalic);

internal sealed record PdfPositionedImage(
    string DataUri,
    double Left,
    double Top,
    double Width,
    double Height);
