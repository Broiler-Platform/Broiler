using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Broiler.Documents.Model;
using Broiler.Graphics;

namespace Broiler.Documents.Rtf;

/// <summary>
/// Serializes a <see cref="RichTextDocument"/> to portable, ASCII-safe RTF. Font
/// and color tables are built from the styles actually used; each styled run is
/// group-wrapped so formatting never leaks; non-ASCII characters are escaped as
/// <c>\uN?</c> with <c>\uc1</c> (surrogate-safe); hyperlinks are written as
/// <c>\field</c>. A <c>\par</c> is emitted after every paragraph, which round-trips
/// exactly through <see cref="RtfReader"/>'s terminator semantics.
/// </summary>
public static class RtfWriter
{
    public static DocumentWriteResult Write(
        RichTextDocument document,
        Stream destination,
        DocumentWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        _ = options;

        var fonts = new ResourceTable<string>(StringComparer.Ordinal);
        var colors = new ResourceTable<BColor>(EqualityComparer<BColor>.Default);
        CollectResources(document, fonts, colors);

        var sb = new StringBuilder();
        sb.Append("{\\rtf1\\ansi\\ansicpg1252\\deff0\\uc1");
        WriteFontTable(sb, fonts);
        WriteColorTable(sb, colors);

        foreach (RichTextParagraph paragraph in document.Paragraphs)
        {
            sb.Append("\\pard\\plain");
            WriteParagraphProperties(sb, paragraph.Style);
            sb.Append(' ');
            WriteRuns(sb, paragraph, fonts, colors);
            sb.Append("\\par\n");
        }

        sb.Append('}');

        byte[] bytes = Encoding.ASCII.GetBytes(sb.ToString());
        destination.Write(bytes, 0, bytes.Length);
        return new DocumentWriteResult(bytes.Length);
    }

    /// <summary>Serialize to a byte array (convenience over the stream overload).</summary>
    public static byte[] WriteToArray(RichTextDocument document, DocumentWriteOptions? options = null)
    {
        using var stream = new MemoryStream();
        Write(document, stream, options);
        return stream.ToArray();
    }

    private static void CollectResources(
        RichTextDocument document,
        ResourceTable<string> fonts,
        ResourceTable<BColor> colors)
    {
        foreach (RichTextParagraph paragraph in document.Paragraphs)
        {
            foreach (StyleRun run in paragraph.Runs)
            {
                InlineStyle style = run.Style;
                if (style.FontFamily is not null)
                    fonts.Intern(style.FontFamily);
                if (!style.Foreground.IsEmpty)
                    colors.Intern(style.Foreground);
                if (!style.Background.IsEmpty)
                    colors.Intern(style.Background);
            }
        }
    }

    private static void WriteFontTable(StringBuilder sb, ResourceTable<string> fonts)
    {
        sb.Append("{\\fonttbl{\\f0\\fnil ;}");
        IReadOnlyList<string> families = fonts.Ordered;
        for (int i = 0; i < families.Count; i++)
        {
            sb.Append("{\\f").Append(i + 1).Append("\\fnil ");
            AppendEscaped(sb, families[i]);
            sb.Append(";}");
        }

        sb.Append('}');
    }

    private static void WriteColorTable(StringBuilder sb, ResourceTable<BColor> colors)
    {
        sb.Append("{\\colortbl;");
        foreach (BColor color in colors.Ordered)
        {
            sb.Append("\\red").Append(color.R)
              .Append("\\green").Append(color.G)
              .Append("\\blue").Append(color.B)
              .Append(';');
        }

        sb.Append('}');
    }

    private static void WriteParagraphProperties(StringBuilder sb, ParagraphStyle style)
    {
        switch (style.Alignment)
        {
            case TextAlignment.Center: sb.Append("\\qc"); break;
            case TextAlignment.Right: sb.Append("\\qr"); break;
            default: break; // Left is the \pard default.
        }

        if (style.IndentLevel > 0)
            sb.Append("\\li").Append(style.IndentLevel * 360);
        if (style.SpacingBefore != 0f)
            sb.Append("\\sb").Append(Twips(style.SpacingBefore));
        if (style.SpacingAfter != 0f)
            sb.Append("\\sa").Append(Twips(style.SpacingAfter));
    }

    private static void WriteRuns(
        StringBuilder sb,
        RichTextParagraph paragraph,
        ResourceTable<string> fonts,
        ResourceTable<BColor> colors)
    {
        int offset = 0;
        foreach (StyleRun run in paragraph.Runs)
        {
            string text = paragraph.Text.Substring(offset, run.Length);
            offset += run.Length;
            WriteRun(sb, text, run.Style, fonts, colors);
        }
    }

    private static void WriteRun(
        StringBuilder sb,
        string text,
        InlineStyle style,
        ResourceTable<string> fonts,
        ResourceTable<BColor> colors)
    {
        if (!string.IsNullOrEmpty(style.LinkHref))
        {
            sb.Append("{\\field{\\*\\fldinst{HYPERLINK \"");
            AppendEscaped(sb, style.LinkHref);
            sb.Append("\"}}{\\fldrslt ");
            WriteStyledText(sb, text, style, fonts, colors);
            sb.Append("}}");
            return;
        }

        WriteStyledText(sb, text, style, fonts, colors);
    }

    private static void WriteStyledText(
        StringBuilder sb,
        string text,
        InlineStyle style,
        ResourceTable<string> fonts,
        ResourceTable<BColor> colors)
    {
        string format = FormatControlWords(style, fonts, colors);
        if (format.Length == 0)
        {
            AppendEscaped(sb, text);
            return;
        }

        sb.Append('{').Append(format).Append(' ');
        AppendEscaped(sb, text);
        sb.Append('}');
    }

    private static string FormatControlWords(
        InlineStyle style,
        ResourceTable<string> fonts,
        ResourceTable<BColor> colors)
    {
        var b = new StringBuilder();
        if (style.Bold) b.Append("\\b");
        if (style.Italic) b.Append("\\i");
        if (style.Underline) b.Append("\\ul");
        if (style.Strikethrough) b.Append("\\strike");
        if (style.FontFamily is not null)
            b.Append("\\f").Append(fonts.IndexOf(style.FontFamily));
        if (style.FontSize.HasValue)
            b.Append("\\fs").Append((int)Math.Round(style.FontSize.Value * 2f));
        if (!style.Foreground.IsEmpty)
            b.Append("\\cf").Append(colors.IndexOf(style.Foreground));
        if (!style.Background.IsEmpty)
            b.Append("\\highlight").Append(colors.IndexOf(style.Background));
        return b.ToString();
    }

    private static void AppendEscaped(StringBuilder sb, string text)
    {
        foreach (char c in text)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '{': sb.Append("\\{"); break;
                case '}': sb.Append("\\}"); break;
                case '\t': sb.Append("\\tab "); break;
                case (char)0x2028: sb.Append("\\line "); break;
                default:
                    if (c is >= (char)0x20 and <= (char)0x7E)
                    {
                        sb.Append(c);
                    }
                    else
                    {
                        int n = c > 32767 ? c - 65536 : c;
                        sb.Append("\\u").Append(n.ToString(CultureInfo.InvariantCulture)).Append('?');
                    }

                    break;
            }
        }
    }

    private static int Twips(float points) => (int)Math.Round(points * 20f);

    private sealed class ResourceTable<T>
        where T : notnull
    {
        // Index 0 is reserved (default font / auto color); interned entries start at 1.
        private readonly Dictionary<T, int> _index;
        private readonly List<T> _ordered = [];

        public ResourceTable(IEqualityComparer<T> comparer) => _index = new Dictionary<T, int>(comparer);

        public IReadOnlyList<T> Ordered => _ordered;

        public void Intern(T value)
        {
            if (_index.ContainsKey(value))
                return;
            _ordered.Add(value);
            _index[value] = _ordered.Count; // 1-based (0 reserved)
        }

        public int IndexOf(T value) => _index.TryGetValue(value, out int i) ? i : 0;
    }
}
