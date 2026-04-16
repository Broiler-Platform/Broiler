using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using PdfPigPoint = UglyToad.PdfPig.Core.PdfPoint;
using PdfPigRectangle = UglyToad.PdfPig.Core.PdfRectangle;

namespace Broiler.Pdf.Tests;

internal static class PdfTestCorpus
{
    internal static string GetManifestPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Corpus", "native-parser-m0-corpus.json");
    }

    internal static byte[] CreateFixtureBytes(string fixtureId)
    {
        return fixtureId switch
        {
            "simple-text-generated" => Encoding.ASCII.GetBytes(CreateMinimalPdf("Hello PDF")),
            "multi-page-generated" => CreateMultiPagePdf("Page 1", "Page 2"),
            "image-heavy-generated" => CreatePdfWithImageAndText(),
            "malformed-generated" => Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\nstartxref\n999999\n%%EOF\n"),
            _ => throw new ArgumentOutOfRangeException(nameof(fixtureId), fixtureId, "Unknown PDF corpus fixture id.")
        };
    }

    private static string CreateMinimalPdf(string text)
    {
        return CreateMinimalPdf((text, 72, 720, 18));
    }

    private static string CreateMinimalPdf(params (string Text, int X, int Y, int FontSize)[] fragments)
    {
        var pageContent = string.Join(
            "\n",
            fragments.Select(fragment =>
            {
                var escapedText = fragment.Text
                    .Replace(@"\", @"\\", StringComparison.Ordinal)
                    .Replace("(", @"\(", StringComparison.Ordinal)
                    .Replace(")", @"\)", StringComparison.Ordinal);

                return $"BT\n/F1 {fragment.FontSize} Tf\n{fragment.X} {fragment.Y} Td\n({escapedText}) Tj\nET";
            })) + "\n";

        var objects = new[]
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Count 1 /Kids [3 0 R] >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n",
            $"4 0 obj\n<< /Length {Encoding.ASCII.GetByteCount(pageContent)} >>\nstream\n{pageContent}endstream\nendobj\n",
            "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n",
        };

        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");

        var offsets = new List<int> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(obj);
        }

        var startXref = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n");
        builder.Append($"0 {objects.Length + 1}\n");
        builder.Append("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            builder.Append(offset.ToString("D10"));
            builder.Append(" 00000 n \n");
        }

        builder.Append("trailer\n");
        builder.Append($"<< /Size {objects.Length + 1} /Root 1 0 R >>\n");
        builder.Append("startxref\n");
        builder.Append(startXref);
        builder.Append("\n%%EOF\n");
        return builder.ToString();
    }

    private static byte[] CreateMultiPagePdf(params string[] pageTexts)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        foreach (var pageText in pageTexts)
        {
            var page = builder.AddPage(612, 792);
            page.AddText(pageText, 18, new PdfPigPoint(72, 720), font);
        }

        return builder.Build();
    }

    private static byte[] CreatePdfWithImageAndText()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(612, 792);
        page.AddText("Image Layout", 18, new PdfPigPoint(72, 720), font);
        page.AddPng(CreateSolidColorPng(2, 2, 0x22, 0x88, 0xCC), new PdfPigRectangle(100, 200, 160, 260));
        return builder.Build();
    }

    private static byte[] CreateSolidColorPng(int width, int height, byte red, byte green, byte blue, byte alpha = 0xFF)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), (uint)height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        WritePngChunk(output, "IHDR", ihdr);

        using var rawImageData = new MemoryStream();
        for (int y = 0; y < height; y++)
        {
            rawImageData.WriteByte(0);
            for (int x = 0; x < width; x++)
            {
                rawImageData.WriteByte(red);
                rawImageData.WriteByte(green);
                rawImageData.WriteByte(blue);
                rawImageData.WriteByte(alpha);
            }
        }

        using var compressedImageData = new MemoryStream();
        using (var zlib = new ZLibStream(compressedImageData, CompressionLevel.SmallestSize, true))
        {
            rawImageData.Position = 0;
            rawImageData.CopyTo(zlib);
        }

        WritePngChunk(output, "IDAT", compressedImageData.ToArray());
        WritePngChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void WritePngChunk(Stream output, string chunkType, byte[] chunkData)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, (uint)chunkData.Length);
        output.Write(lengthBytes);

        var chunkTypeBytes = Encoding.ASCII.GetBytes(chunkType);
        output.Write(chunkTypeBytes);
        output.Write(chunkData);

        var crcBuffer = new byte[chunkTypeBytes.Length + chunkData.Length];
        Buffer.BlockCopy(chunkTypeBytes, 0, crcBuffer, 0, chunkTypeBytes.Length);
        Buffer.BlockCopy(chunkData, 0, crcBuffer, chunkTypeBytes.Length, chunkData.Length);

        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, ComputeCrc32(crcBuffer));
        output.Write(crcBytes);
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var value in data)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 1) == 0 ? crc >> 1 : 0xEDB88320u ^ (crc >> 1);
        }

        return ~crc;
    }
}
