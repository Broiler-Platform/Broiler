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
    /// The paragraph above is true of the HtmlBridge DOM path, which publishes this on every parse.
    /// It is <b>not</b> true of the HTML-string render path, which never writes here at all — see
    /// <see cref="AmbientRenderState"/>, whose assertion exists to make that visible the moment a
    /// second thread is involved.
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
