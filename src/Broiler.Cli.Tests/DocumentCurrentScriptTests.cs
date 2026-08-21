using Broiler.HtmlBridge.Scripting;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>document.currentScript</c> — the element whose classic script is running.
/// <para>
/// Reported against <c>https://www.google.com/intl/de/about.html</c>, where <c>google.com</c>'s
/// "About" link leads: <c>TypeError: Cannot get property src of undefined … at inline-0:line 14</c>.
/// Line 14 of that page's first script is Google's tag-manager loader reading
/// <c>new URL(document.currentScript.src).searchParams</c>, and the property was not bound, so it
/// read <c>undefined</c> and the dereference threw. The next two lines are that script's
/// <c>const id</c> and <c>const cookieCategory</c>, so stopping there also left the cookie bar's
/// callback reading <c>id</c> in its temporal dead zone — one missing property, two failures, and
/// no analytics on the page.
/// </para>
/// <para>
/// The element it has to name is the second thing this covers. The document's first
/// <c>&lt;script&gt;</c> on that page is a JSON-LD data block, which no browser executes, and the
/// hosts paired the <em>n</em>-th executed script with the <em>n</em>-th <c>&lt;script&gt;</c>
/// element — so the first script that ran would have been attributed to the JSON-LD, whose
/// <c>src</c> is absent. Naming the wrong element is worse than naming none.
/// </para>
/// </summary>
public sealed class DocumentCurrentScriptTests
{
    /// <summary>
    /// The reported line, on the reported page's shape: a JSON-LD data block, then a script with a
    /// <c>src</c> that reads its own URL out of <c>document.currentScript</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AScriptReadsItsOwnSrc_PastTheDataBlockInFrontOfIt()
    {
        // `new URL(document.currentScript.src)` — the reported expression, over a data: URI so the
        // test needs no network. A data: URI is its own absolute URL, so it reaches the URL parser
        // exactly as the loader's `https://…/gtm.js?id=…` does.
        const string Loader = """
            const url = new URL(document.currentScript.src);
            const out = document.createElement('div');
            out.id = 'result';
            out.textContent = 'element=' + document.currentScript.id + '|url=' + url.href.slice(0, 21);
            document.body.appendChild(out);
            """;

        var html = $$"""
            <!DOCTYPE html>
            <html><head>
            <script type="application/ld+json">{"@context":"http://schema.org","@type":"WebSite"}</script>
            <script id="loader" src="data:text/javascript,{{Uri.EscapeDataString(Loader)}}"></script>
            </head><body></body></html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("element=loader|url=data:text/javascript", result);
    }

    /// <summary>
    /// Between scripts the property is <c>null</c> — the value a page can test — rather than
    /// <c>undefined</c>, which is what an unbound property reads as and what the reported script
    /// dereferenced.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void OutsideAScriptItIsNullAndNotUndefined()
    {
        const string Html = """
            <!DOCTYPE html>
            <html><body><div id="result"></div>
            <script>
            var out = document.getElementById('result');
            var inScript = document.currentScript === null ? 'null' : 'element';
            setTimeout(function () {
                out.textContent =
                    'bound=' + ('currentScript' in document) +
                    '|inScript=' + inScript +
                    '|inCallback=' + (document.currentScript === null ? 'null' : String(document.currentScript));
            }, 0);
            </script>
            </body></html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(Html, "file:///test.html");

        Assert.Contains("bound=true|inScript=element|inCallback=null", result);
    }

    /// <summary>
    /// A deferred script is the running script for as long as it runs. That bucket never set the
    /// insertion point at all, so <c>currentScript</c> would have stayed <c>null</c> through every
    /// <c>&lt;script defer&gt;</c> on the page — and <c>document.write</c> appended to
    /// <c>&lt;body&gt;</c> rather than writing at the script's position.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ADeferredScriptIsTheCurrentScriptWhileItRuns()
    {
        const string Html = """
            <!DOCTYPE html>
            <html><body><div id="result"></div>
            <script>window.order = [];</script>
            <script id="late" defer>
            window.order.push('deferred:' + (document.currentScript ? document.currentScript.id : 'null'));
            document.getElementById('result').textContent = window.order.join(',');
            </script>
            </body></html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(Html, "file:///test.html");

        Assert.Contains("deferred:late", result);
    }

    /// <summary>
    /// The same misalignment under <c>document.write</c>, which uses the identical insertion point:
    /// the written fragment belongs immediately after the script that wrote it, and with a data
    /// block counted in front of it the position came from a different element.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void DocumentWriteLandsAfterTheScriptThatWroteIt_PastADataBlock()
    {
        const string Html = """
            <!DOCTYPE html>
            <html><body>
            <script type="application/ld+json">{"@type":"WebSite"}</script>
            <p id="before">before</p>
            <script>document.write('<p id="written">written</p>');</script>
            <p id="after">after</p>
            </body></html>
            """;

        var result = CaptureService.ExecuteScriptsWithDom(Html, "file:///test.html");

        var written = result.IndexOf("id=\"written\"", StringComparison.Ordinal);
        var after = result.IndexOf("id=\"after\"", StringComparison.Ordinal);
        var before = result.IndexOf("id=\"before\"", StringComparison.Ordinal);

        Assert.True(written > before, "the written fragment belongs after the <p> preceding the script");
        Assert.True(written < after, "the written fragment belongs before the <p> following the script");
    }

    /// <summary>
    /// The mapping itself, stated over the element list: a data block is not a script element any
    /// bucket runs, a module belongs to neither classic bucket, and <c>defer</c> decides which of
    /// the two an element is counted in.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheElementMapCountsOnlyWhatEachBucketRuns()
    {
        const string Html = """
            <!DOCTYPE html>
            <html><body>
            <script type="application/ld+json">{}</script>
            <script id="a"></script>
            <script type="module" id="m"></script>
            <script id="b" defer></script>
            <script id="c" type="text/javascript"></script>
            <script id="d" defer type="application/javascript"></script>
            <script type="text/template">- not javascript</script>
            </body></html>
            """;

        using var bridge = new Broiler.HtmlBridge.DomBridge();
        using var context = new Broiler.JavaScript.Engine.JSContext();
        bridge.Attach(context, Html, "file:///test.html");

        static string Ids(Broiler.HtmlBridge.DomBridge bridge, IReadOnlyList<int> indices) =>
            string.Join(",", indices.Select(i => bridge.Elements[i].GetAttribute("id") ?? "?"));

        Assert.Equal("a,c", Ids(bridge, ScriptElementMap.Classic(bridge.Elements)));
        Assert.Equal("b,d", Ids(bridge, ScriptElementMap.Deferred(bridge.Elements)));
    }
}
