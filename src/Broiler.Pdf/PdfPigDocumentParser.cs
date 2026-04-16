using UglyToad.PdfPig.Content;
using PdfPigDocumentSource = UglyToad.PdfPig.PdfDocument;

namespace Broiler.Pdf;

internal sealed class PdfPigDocumentParser : IPdfDocumentParser
{
    public IPdfDocument Open(string path)
    {
        return new PdfPigBackedDocument(PdfPigDocumentSource.Open(path));
    }

    private sealed class PdfPigBackedDocument(PdfPigDocumentSource document) : IPdfDocument
    {
        public IReadOnlyList<IPdfPage> Pages { get; } = document
            .GetPages()
            .Select(page => (IPdfPage)new PdfPigBackedPage(page))
            .ToList();

        public void Dispose()
        {
            document.Dispose();
        }
    }

    private sealed class PdfPigBackedPage(Page page) : IPdfPage
    {
        public int Number => page.Number;

        public string Text => page.Text;

        public PdfRectangle MediaBox => new(0, 0, page.Width, page.Height);

        public PdfPageLayout ExtractLayout()
        {
            var images = new List<PdfPositionedImage>();
            foreach (var image in page.GetImages())
            {
                if (!TryBuildImageDataUri(image, out var dataUri))
                    continue;

                var boundingBox = image.BoundingBox;
                images.Add(
                    new PdfPositionedImage(
                        dataUri,
                        Math.Max(0, boundingBox.Left),
                        Math.Max(0, page.Height - boundingBox.Top),
                        Math.Max(1, boundingBox.Width),
                        Math.Max(1, boundingBox.Height)));
            }

            var text = new List<PdfPositionedText>();
            foreach (var letter in page.Letters)
            {
                if (string.IsNullOrWhiteSpace(letter.Value))
                    continue;

                text.Add(
                    new PdfPositionedText(
                        letter.Value,
                        Math.Max(0, letter.BoundingBox.Left),
                        Math.Max(0, page.Height - letter.BoundingBox.Top),
                        Math.Max(1, letter.PointSize > 0 ? letter.PointSize : letter.FontSize),
                        letter.FontName,
                        IsBoldFont(letter.FontName),
                        IsItalicFont(letter.FontName)));
            }

            return new PdfPageLayout(text, images);
        }
    }

    private static bool TryBuildImageDataUri(IPdfImage image, out string dataUri)
    {
        if (image.TryGetPng(out var pngBytes) && pngBytes is { Length: > 0 })
        {
            dataUri = BuildDataUri("image/png", pngBytes);
            return true;
        }

        if (image.TryGetBytesAsMemory(out var imageBytes)
            && TryDetectEmbeddedImageMimeType(imageBytes.Span, out var mimeType))
        {
            dataUri = BuildDataUri(mimeType, imageBytes.ToArray());
            return true;
        }

        dataUri = string.Empty;
        return false;
    }

    private static string BuildDataUri(string mimeType, byte[] imageBytes)
    {
        return $"data:{mimeType};base64,{Convert.ToBase64String(imageBytes)}";
    }

    private static bool TryDetectEmbeddedImageMimeType(ReadOnlySpan<byte> data, out string mimeType)
    {
        if (data.Length >= 8
            && data[0] == 0x89
            && data[1] == 0x50
            && data[2] == 0x4E
            && data[3] == 0x47
            && data[4] == 0x0D
            && data[5] == 0x0A
            && data[6] == 0x1A
            && data[7] == 0x0A)
        {
            mimeType = "image/png";
            return true;
        }

        if (data.Length >= 3
            && data[0] == 0xFF
            && data[1] == 0xD8
            && data[2] == 0xFF)
        {
            mimeType = "image/jpeg";
            return true;
        }

        if (data.Length >= 6
            && data[0] == 0x47
            && data[1] == 0x49
            && data[2] == 0x46
            && data[3] == 0x38
            && (data[4] == 0x37 || data[4] == 0x39)
            && data[5] == 0x61)
        {
            mimeType = "image/gif";
            return true;
        }

        if (data.Length >= 2
            && data[0] == 0x42
            && data[1] == 0x4D)
        {
            mimeType = "image/bmp";
            return true;
        }

        mimeType = string.Empty;
        return false;
    }

    private static bool IsBoldFont(string? fontName)
    {
        return !string.IsNullOrWhiteSpace(fontName)
            && fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsItalicFont(string? fontName)
    {
        return !string.IsNullOrWhiteSpace(fontName)
            && (fontName.Contains("Italic", StringComparison.OrdinalIgnoreCase)
                || fontName.Contains("Oblique", StringComparison.OrdinalIgnoreCase));
    }
}
