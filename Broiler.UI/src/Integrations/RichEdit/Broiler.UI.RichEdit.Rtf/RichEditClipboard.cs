using System;
using System.Text;
using Broiler.Documents.Model;
using Broiler.Documents.Rtf;

namespace Broiler.UI.RichEdit.Rtf;

/// <summary>
/// Rich copy/paste for <see cref="UiRichEdit"/> using the RTF codec. Copy serializes
/// the selection to RTF (plus plain text); paste prefers RTF — read through
/// <see cref="RtfReader"/>, which applies the safe read policy (ADR 0004:
/// objects skipped, disallowed link schemes dropped, limits enforced) — and falls
/// back to plain text. This lives outside the core control (ADR 0016/0018); the
/// core is never coupled to a document-format codec.
/// </summary>
public static class RichEditClipboard
{
    /// <summary>Copies the current selection to <paramref name="host"/> as RTF + plain text.</summary>
    public static bool Copy(UiRichEdit richEdit, IUiRichClipboardHost host)
    {
        ArgumentNullException.ThrowIfNull(richEdit);
        ArgumentNullException.ThrowIfNull(host);

        RichTextRange selection = richEdit.Selection;
        if (selection.IsEmpty)
            return false;

        RichTextDocument slice = richEdit.Document.Slice(selection);
        host.SetRichContent(slice.PlainText, DocumentToRtf(slice));
        return true;
    }

    /// <summary>
    /// Pastes from <paramref name="host"/>: RTF (sanitized) when present, otherwise
    /// plain text. Returns false when the editor is read-only/disabled or the
    /// clipboard is empty.
    /// </summary>
    public static bool Paste(UiRichEdit richEdit, IUiRichClipboardHost host)
    {
        ArgumentNullException.ThrowIfNull(richEdit);
        ArgumentNullException.ThrowIfNull(host);

        if (host.TryGetRtf(out string rtf) && !string.IsNullOrEmpty(rtf))
            return InsertRtf(richEdit, rtf);
        if (host.TryGetPlainText(out string text) && !string.IsNullOrEmpty(text))
            return richEdit.ExecuteCommand(RichEditCommand.InsertText, text);
        return false;
    }

    /// <summary>Serializes the current selection to RTF, or the empty string if there is no selection.</summary>
    public static string SelectionToRtf(UiRichEdit richEdit)
    {
        ArgumentNullException.ThrowIfNull(richEdit);
        RichTextRange selection = richEdit.Selection;
        return selection.IsEmpty ? string.Empty : DocumentToRtf(richEdit.Document.Slice(selection));
    }

    /// <summary>Serializes a whole document to an RTF string.</summary>
    public static string DocumentToRtf(RichTextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Encoding.ASCII.GetString(RtfWriter.WriteToArray(document));
    }

    /// <summary>
    /// Reads (sanitizing) an RTF string and inserts its rich content at the caret,
    /// replacing the selection. This is the safe rich-paste entry point.
    /// </summary>
    public static bool InsertRtf(UiRichEdit richEdit, string rtf)
    {
        ArgumentNullException.ThrowIfNull(richEdit);
        if (string.IsNullOrEmpty(rtf))
            return false;

        RichTextDocument document = RtfReader.Read(Encoding.Latin1.GetBytes(rtf)).Document;
        return richEdit.InsertDocument(document);
    }
}
