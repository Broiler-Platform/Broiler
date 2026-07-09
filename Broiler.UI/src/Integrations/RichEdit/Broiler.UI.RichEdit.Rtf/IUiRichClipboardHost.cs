namespace Broiler.UI.RichEdit.Rtf;

/// <summary>
/// A clipboard host that carries both a plain-text and a rich (RTF) representation.
/// This is the optional rich counterpart to the core text-only
/// <c>Broiler.UI.IUiClipboardHost</c> (ADR 0016): the core control never references
/// it, so core RichEdit stays codec-free. A host implements this over the OS
/// clipboard (setting/getting <c>CF_TEXT</c> and <c>CF_RTF</c>); tests implement it
/// in memory.
/// </summary>
public interface IUiRichClipboardHost
{
    /// <summary>Places both representations on the clipboard together.</summary>
    void SetRichContent(string plainText, string rtf);

    /// <summary>Gets the RTF payload if the clipboard carries one.</summary>
    bool TryGetRtf(out string rtf);

    /// <summary>Gets the plain-text payload if the clipboard carries one.</summary>
    bool TryGetPlainText(out string text);
}
