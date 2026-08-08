using System.Text.RegularExpressions;

namespace Broiler.Layout;

/// <summary>
/// Per-render (thread-local) document-mode state, mirroring the way
/// <c>Broiler.CSS.CssLengthParser</c> exposes the current viewport size for the
/// duration of a render. The host publishes the document's quirks-mode flag here
/// when it parses the page, so layout — which on the HTML-string parse path holds
/// no reference back to the source document — can consult it while sizing the
/// root and body boxes (the quirks-mode body/html fill-viewport behaviour,
/// https://quirks.spec.whatwg.org/).
///
/// Layout runs on the same thread immediately after the parse that set this, and
/// each parse overwrites it, so a stale <c>true</c> never leaks into a later
/// standards-mode render. <c>CssBox</c> additionally caches the value on the tree
/// root the first time it reads it, so it survives a re-layout pass that may run
/// on a different thread.
/// </summary>
public static partial class DocumentModeContext
{
    [ThreadStatic]
    private static bool _quirksMode;

    /// <summary>The quirks-mode flag of the document currently being laid out.</summary>
    /// <remarks>
    /// Both render paths publish this on every parse: the HtmlBridge DOM path at
    /// <c>DomBridge.HtmlParsing.cs</c>, and — since Phase 2 item #9 — the HTML-string path at
    /// <c>HtmlContainerInt.SetHtmlWithStyleSet</c>. Until the second of those existed the flag was
    /// simply never written on the string path, which was harmless only because one thread rendered
    /// one document at a time; a pooled thread arriving from a quirks-mode DOM render would have
    /// carried that <c>true</c> into a standards-mode string render. <see cref="AmbientRenderState"/>
    /// is the check that would have caught it, and now has nothing to catch here.
    /// </remarks>
    public static bool CurrentQuirksMode
    {
        get
        {
            AmbientRenderState.AssertEstablished(
                AmbientRenderState.Slots.DocumentMode, nameof(CurrentQuirksMode));
            return _quirksMode;
        }

        set
        {
            _quirksMode = value;
            AmbientRenderState.MarkEstablished(AmbientRenderState.Slots.DocumentMode);
        }
    }

    /// <summary>
    /// Lightweight quirks-mode determination from raw HTML. A document with no
    /// doctype, or any doctype whose name is not a bare <c>html</c>, is in quirks
    /// mode; a bare <c>&lt;!DOCTYPE html&gt;</c> selects standards mode.
    /// </summary>
    public static bool IsQuirksHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return true;

        var match = QuirksRegex().Match(html);
        return !(match.Success
            && match.Groups[1].Value.Equals("html", StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"<!doctype\s+([^\s>]+)", RegexOptions.IgnoreCase)]
    private static partial Regex QuirksRegex();
}
