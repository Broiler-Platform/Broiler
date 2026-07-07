using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Broiler.Documents.Model;

namespace Broiler.Documents.Markdown;

/// <summary>Serializes the rich-text model to deterministic UTF-8 Markdown.</summary>
public static class MarkdownWriter
{
    public static DocumentWriteResult Write(
        RichTextDocument document,
        Stream destination,
        DocumentWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        _ = options;

        var diagnostics = new List<DocumentDiagnostic>();
        var builder = new StringBuilder();
        for (int i = 0; i < document.ParagraphCount; i++)
        {
            if (i > 0)
                builder.Append("\n\n");

            WriteParagraph(builder, document.Paragraphs[i], diagnostics);
        }

        builder.Append('\n');
        byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
        destination.Write(bytes, 0, bytes.Length);
        return new DocumentWriteResult(bytes.Length, diagnostics);
    }

    public static byte[] WriteToArray(RichTextDocument document, DocumentWriteOptions? options = null)
    {
        using var stream = new MemoryStream();
        Write(document, stream, options);
        return stream.ToArray();
    }

    private static void WriteParagraph(
        StringBuilder builder,
        RichTextParagraph paragraph,
        List<DocumentDiagnostic> diagnostics)
    {
        ParagraphStyle style = paragraph.Style;
        if (style.IndentLevel > 0)
            builder.Append(new string(' ', Math.Max(0, style.IndentLevel - 1) * 2));

        if (style.ListKind == ListKind.Bullet)
            builder.Append("- ");
        else if (style.ListKind == ListKind.Numbered)
            builder.Append("1. ");

        if (style.Alignment != TextAlignment.Left ||
            Math.Abs(style.LineSpacing - 1f) > 0.001f ||
            Math.Abs(style.SpacingBefore) > 0.001f ||
            Math.Abs(style.SpacingAfter) > 0.001f)
        {
            diagnostics.Add(DocumentDiagnostic.Warning(
                "markdown.paragraph-style",
                "Markdown writer dropped paragraph alignment, line spacing, or spacing values."));
        }

        int offset = 0;
        foreach (StyleRun run in paragraph.Runs)
        {
            string text = paragraph.Text.Substring(offset, run.Length);
            offset += run.Length;
            builder.Append(FormatRun(text, run.Style, diagnostics));
        }
    }

    private static string FormatRun(
        string text,
        InlineStyle style,
        List<DocumentDiagnostic> diagnostics)
    {
        string formatted = EscapeText(text);

        if (style.FontFamily is not null && style.FontFamily.Equals("monospace", StringComparison.OrdinalIgnoreCase))
            formatted = "`" + formatted.Replace("`", "\\`", StringComparison.Ordinal) + "`";
        else if (style.FontFamily is not null)
            diagnostics.Add(DocumentDiagnostic.Warning("markdown.inline-style", "Markdown writer dropped a non-monospace font family."));

        if (style.Strikethrough)
            formatted = "~~" + formatted + "~~";
        if (style.Italic)
            formatted = "*" + formatted + "*";
        if (style.Bold)
            formatted = "**" + formatted + "**";
        if (style.LinkHref is not null)
            formatted = "[" + formatted + "](" + EscapeLinkDestination(style.LinkHref) + ")";

        if (style.Underline ||
            style.FontSize.HasValue ||
            !style.Foreground.IsEmpty ||
            !style.Background.IsEmpty)
        {
            diagnostics.Add(DocumentDiagnostic.Warning(
                "markdown.inline-style",
                "Markdown writer dropped underline, size, foreground, or background styling."));
        }

        return formatted;
    }

    private static string EscapeText(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            switch (c)
            {
                case '\\':
                case '`':
                case '*':
                case '_':
                case '[':
                case ']':
                case '(':
                case ')':
                case '#':
                case '+':
                case '-':
                case '.':
                case '!':
                    builder.Append('\\').Append(c);
                    break;
                case (char)0x2028:
                    builder.Append("  \n");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    private static string EscapeLinkDestination(string href) =>
        href.Replace(")", "%29", StringComparison.Ordinal).Replace(" ", "%20", StringComparison.Ordinal);
}
