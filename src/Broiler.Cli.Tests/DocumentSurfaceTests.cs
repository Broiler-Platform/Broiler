using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;

namespace Broiler.Cli.Tests;

/// <summary>
/// The document surfaces that were absent from the script-visible document: <c>charset</c>,
/// <c>referrer</c>, <c>domain</c>, <c>lastModified</c>, <c>activeElement</c>, <c>hasFocus()</c>, and
/// the <c>onvisibilitychange</c> handler slot.
/// </summary>
/// <remarks>
/// Each read <c>undefined</c> (or, for <c>hasFocus</c>, was missing outright), which is not the same
/// as answering "none": a page comparing <c>document.domain === location.hostname</c>, stringifying
/// <c>document.referrer</c> into a beacon, or calling <c>new Date(document.lastModified)</c> saw a
/// third state it had no branch for. Every value here is the one the specification gives for a
/// directly-navigated, permanently-visible, unfocused-by-nobody capture rather than a placeholder —
/// see the registration comments in DomBridge/Registration/Document.cs.
/// <para>
/// <c>window.trustedTypes</c> was audited alongside these and deliberately left absent: it is a whole
/// enforcement API, and a shape-only stub would claim a policy mechanism that does not exist.
/// </para>
/// </remarks>
public class DocumentSurfaceTests
{
    private static string ExecJs(string jsCode, string url = "https://example.com/page")
    {
        var html = $@"<!doctype html>
<html><head><title>Test</title></head>
<body><div id=""result""></div>
<script>
{jsCode}
</script>
</body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, url);
    }

    // charset is the third historical spelling of characterSet (DOM §4.5); all three name one value.
    [Fact(Timeout = 600000)]
    public void Document_Charset_Agrees_With_CharacterSet_And_InputEncoding()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent =
                'CS:' + document.charset +
                ',AGREE:' + (document.charset === document.characterSet && document.charset === document.inputEncoding);
        ");
        Assert.Contains("CS:UTF-8", result);
        Assert.Contains("AGREE:true", result);
    }

    // A capture navigates directly, so it has no referrer — which HTML reports as the empty string,
    // not as an absent property.
    [Fact(Timeout = 600000)]
    public void Document_Referrer_Is_The_Empty_String_Not_Undefined()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent =
                'TYPE:' + typeof document.referrer + ',EMPTY:' + (document.referrer === '');
        ");
        Assert.Contains("TYPE:string", result);
        Assert.Contains("EMPTY:true", result);
    }

    // document.domain is the origin's effective domain — this document's host.
    [Fact(Timeout = 600000)]
    public void Document_Domain_Is_The_Page_Host()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent =
                'D:' + document.domain + ',MATCHES:' + (document.domain === location.hostname);
        ");
        Assert.Contains("D:example.com", result);
        Assert.Contains("MATCHES:true", result);
    }

    // A URL with no host has an opaque origin, whose effective domain is reported as "".
    [Fact(Timeout = 600000)]
    public void Document_Domain_Is_Empty_For_An_Opaque_Origin()
    {
        var result = ExecJs(
            "document.getElementById('result').textContent = 'TYPE:' + typeof document.domain + ',EMPTY:' + (document.domain === '');",
            "data:text/html,<p>x");
        Assert.Contains("TYPE:string", result);
        Assert.Contains("EMPTY:true", result);
    }

    // The value's common use is `new Date(document.lastModified)`, so the MM/DD/YYYY hh:mm:ss shape
    // matters as much as its presence.
    [Fact(Timeout = 600000)]
    public void Document_LastModified_Has_The_Specified_Format_And_Parses()
    {
        var result = ExecJs(@"
            var lm = document.lastModified;
            document.getElementById('result').textContent =
                'SHAPE:' + /^\d{2}\/\d{2}\/\d{4} \d{2}:\d{2}:\d{2}$/.test(lm) +
                ',PARSES:' + !isNaN(new Date(lm).getTime());
        ");
        Assert.Contains("SHAPE:true", result);
        Assert.Contains("PARSES:true", result);
    }

    // HTML's algorithm ends "if candidate is null, set candidate to the body element", so an
    // unfocused document reports body rather than null.
    [Fact(Timeout = 600000)]
    public void Document_ActiveElement_Is_The_Body_When_Nothing_Is_Focused()
    {
        var result = ExecJs(@"
            var ae = document.activeElement;
            document.getElementById('result').textContent =
                'TAG:' + (ae ? ae.tagName : 'NULL') + ',ISBODY:' + (ae === document.body);
        ");
        Assert.Contains("TAG:BODY", result);
        Assert.Contains("ISBODY:true", result);
    }

    // hasFocus() is true for the same reason visibilityState is "visible" — one document, one
    // viewport, never backgrounded and never defocused.
    [Fact(Timeout = 600000)]
    public void Document_HasFocus_Is_Callable_And_True()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent =
                'TYPE:' + typeof document.hasFocus + ',VAL:' + document.hasFocus();
        ");
        Assert.Contains("TYPE:function", result);
        Assert.Contains("VAL:true", result);
    }

    // The handler slot completes the Page Visibility pair: `'onvisibilitychange' in document` is the
    // feature test that decides whether a page uses the API at all.
    [Fact(Timeout = 600000)]
    public void Document_OnVisibilityChange_Slot_Exists_Defaults_Null_And_Is_Assignable()
    {
        var result = ExecJs(@"
            var present = 'onvisibilitychange' in document;
            var initial = document.onvisibilitychange;
            document.onvisibilitychange = function () {};
            document.getElementById('result').textContent =
                'IN:' + present + ',NULL:' + (initial === null) + ',SET:' + typeof document.onvisibilitychange;
        ");
        Assert.Contains("IN:true", result);
        Assert.Contains("NULL:true", result);
        Assert.Contains("SET:function", result);
    }

    // The pair these complete must keep answering as before.
    [Fact(Timeout = 600000)]
    public void Page_Visibility_Pair_Is_Unchanged()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent =
                'VS:' + document.visibilityState + ',HIDDEN:' + document.hidden;
        ");
        Assert.Contains("VS:visible", result);
        Assert.Contains("HIDDEN:false", result);
    }
}
