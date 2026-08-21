using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Logging;

namespace Broiler.Cli.Tests;

/// <summary>
/// A <c>&lt;script&gt;</c> the page's own JavaScript builds and inserts has to run, the way it does
/// in a browser. Nothing ran it: capture discovers scripts by scanning the document source once, so
/// an element from <c>document.createElement('script')</c> was added to the tree and then never
/// fetched or executed — and <c>script.src</c> was not even a reflected IDL attribute, so the
/// assignment that gives it a URL wrote a plain JS property and the element serialized bare.
/// </summary>
/// <remarks>
/// The two gaps compound into the reported failure rather than a missing feature: the loader idiom
/// (inject a script, poll until the global it defines appears) never terminates, so the capture
/// spends its whole async-drain budget on a poll that cannot succeed, reports "async work did not
/// settle", and renders a page whose scripts never got started. html5test.com is exactly that shape
/// — its <c>waitForWhichBrowser</c> polls every 100 ms for a global defined by a script it injects.
/// </remarks>
public sealed class ScriptInsertionTests
{
    private const string Url = "https://example.test/page.html";

    /// <summary>Runs the capture pipeline's DOM script path and returns the serialized document.</summary>
    private static string Run(string bodyAndScripts) =>
        CaptureService.ExecuteScriptsWithDom(
            "<!DOCTYPE html><html><head></head><body><div id='out'>none</div>" + bodyAndScripts + "</body></html>",
            Url);

    /// <summary>The text the page's scripts left in <c>#out</c> — how each case reports what ran.</summary>
    private static string OutputOf(string html)
    {
        var marker = html.IndexOf("id=\"out\"", StringComparison.Ordinal);
        if (marker < 0)
            marker = html.IndexOf("id='out'", StringComparison.Ordinal);
        Assert.True(marker >= 0, "the #out element must survive serialization: " + html);
        var start = html.IndexOf('>', marker) + 1;
        return html[start..html.IndexOf('<', start)];
    }

    /// <summary>A <c>data:</c> URI script source — a real external script with no network or disk.</summary>
    private static string DataUri(string source) => "data:text/javascript," + source;

    [Fact(Timeout = 600000)]
    public void ScriptCreatedWithASrcIsFetchedAndExecuted()
    {
        var html = Run(
            $"<script>var s = document.createElement('script');" +
            $"s.src = '{DataUri("window.LOADED = \"yes\";")}';" +
            "document.head.appendChild(s);</script>" +
            // The injected script runs on the event loop, as in a browser, so the observation has to
            // happen after it — a timer is the earliest point the page itself could look.
            "<script>setTimeout(function(){ document.getElementById('out').textContent = 'LOADED=' + window.LOADED; }, 0);</script>");

        Assert.Equal("LOADED=yes", OutputOf(html));
    }

    /// <summary>
    /// The reported failure, reduced: a poll that waits for a global an injected script defines. Both
    /// halves have to work for it to terminate — the <c>src</c> assignment must reach the DOM, and the
    /// inserted element must be fetched and run.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void PollWaitingOnAnInjectedScriptTerminatesInsteadOfExhaustingTheDrainBudget()
    {
        var warnings = new List<string>();
        void Collect(RenderLogEntry entry)
        {
            if (entry.Level >= LogLevel.Warning)
                lock (warnings) warnings.Add(entry.Message);
        }

        RenderLogger.EntryLogged += Collect;
        string html;
        try
        {
            html = Run(
                $"<script>var s = document.createElement('script');" +
                $"s.src = '{DataUri("window.WhichBrowser = function(){};")}';" +
                "document.getElementsByTagName('head')[0].appendChild(s);</script>" +
                "<script>(function wait(){" +
                "  if (typeof WhichBrowser == 'undefined') window.setTimeout(wait, 100);" +
                "  else document.getElementById('out').textContent = 'ready';" +
                "})();</script>");
        }
        finally
        {
            RenderLogger.EntryLogged -= Collect;
        }

        Assert.Equal("ready", OutputOf(html));
        Assert.DoesNotContain(warnings, w => w.Contains("did not settle", StringComparison.Ordinal));
    }

    /// <summary>
    /// An inline script-created script runs synchronously during the insertion, per spec — the line
    /// after the <c>appendChild</c> can already see what it defined. Deferring it would break that.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ScriptCreatedWithInlineTextRunsDuringTheInsertion()
    {
        var html = Run(
            "<script>var s = document.createElement('script');" +
            "s.text = \"window.FROM_INLINE = 'ran';\";" +
            "document.head.appendChild(s);" +
            "document.getElementById('out').textContent = 'FROM_INLINE=' + window.FROM_INLINE;</script>");

        Assert.Equal("FROM_INLINE=ran", OutputOf(html));
    }

    /// <summary>The other order: append the element first, give it a <c>src</c> afterwards.</summary>
    [Fact(Timeout = 600000)]
    public void ASrcAssignedAfterInsertionStillStartsTheScript()
    {
        var html = Run(
            "<script>var s = document.createElement('script');" +
            "document.head.appendChild(s);" +
            $"s.src = '{DataUri("window.LATE = \"ran\";")}';</script>" +
            "<script>setTimeout(function(){ document.getElementById('out').textContent = 'LATE=' + window.LATE; }, 0);</script>");

        Assert.Equal("LATE=ran", OutputOf(html));
    }

    /// <summary>
    /// A loader assigns <c>onload</c> after <c>appendChild</c> at least as often as before it, which
    /// is the reason an external script's fetch is deferred to the event loop rather than run inside
    /// the insertion: fetching there would fire <c>load</c> at a handler that does not exist yet.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheLoadEventReachesAHandlerAssignedAfterTheAppend()
    {
        var html = Run(
            "<script>var s = document.createElement('script');" +
            $"s.src = '{DataUri("window.ORDER = \"script\";")}';" +
            "document.head.appendChild(s);" +
            "s.onload = function(){ document.getElementById('out').textContent = 'onload after ' + window.ORDER; };</script>");

        Assert.Equal("onload after script", OutputOf(html));
    }

    /// <summary>
    /// Per spec a script an <c>innerHTML</c> parse produces is "already started" at birth and never
    /// executes. It must stay that way now that insertion is watched — otherwise every framework that
    /// assigns markup containing a <c>&lt;script&gt;</c> would start executing it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AScriptFromInnerHtmlDoesNotRun()
    {
        var html = Run(
            "<div id='host'></div>" +
            "<script>document.getElementById('host').innerHTML = " +
            "  \"<scr\" + \"ipt>window.FROM_INNER_HTML = 'ran';</scr\" + \"ipt>\";" +
            "document.getElementById('out').textContent = 'FROM_INNER_HTML=' + (window.FROM_INNER_HTML || 'undefined');</script>");

        Assert.Equal("FROM_INNER_HTML=undefined", OutputOf(html));
    }

    /// <summary>
    /// The document's own scripts are run by the host from the document source. The bridge sees those
    /// same elements in the parsed tree and must not run them a second time — a doubled side effect
    /// is worse than the missing one this change fixes.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AParserInsertedScriptIsNotRunASecondTime()
    {
        var html = Run(
            "<script>window.RUNS = (window.RUNS || 0) + 1;" +
            "document.getElementById('out').textContent = 'runs=' + window.RUNS;</script>");

        Assert.Equal("runs=1", OutputOf(html));
    }

    /// <summary>
    /// A <c>type</c> that is not a JavaScript MIME type parks data in a <c>&lt;script&gt;</c> the
    /// browser must never execute — the standard way to ship a client-side template.
    /// </summary>
    [Theory]
    [InlineData("text/template")]
    [InlineData("application/json")]
    public void ScriptCreatedWithANonJavaScriptTypeDoesNotRun(string type)
    {
        var html = Run(
            "<script>var s = document.createElement('script');" +
            $"s.type = '{type}';" +
            "s.text = \"window.PARKED = 'ran';\";" +
            "document.head.appendChild(s);" +
            "document.getElementById('out').textContent = 'PARKED=' + (window.PARKED || 'undefined');</script>");

        Assert.Equal("PARKED=undefined", OutputOf(html));
    }

    /// <summary>
    /// HTMLScriptElement's IDL attributes reflect to content attributes. Without this the assignment
    /// that names the script's URL set a plain JS property: the element serialized as a bare
    /// <c>&lt;script&gt;</c> and there was nothing to fetch.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ScriptIdlAttributesReflectToContentAttributes()
    {
        var html = Run(
            "<script>var s = document.createElement('script');" +
            "s.src = 'lib.js'; s.type = 'text/javascript'; s.async = false; s.defer = true;" +
            "s.integrity = 'sha384-abc';" +
            "document.getElementById('out').textContent = " +
            "  [s.getAttribute('src'), s.getAttribute('type'), s.getAttribute('integrity')," +
            "   String(s.hasAttribute('async')), String(s.async), String(s.defer)].join('|');</script>");

        // A boolean IDL attribute is the attribute's presence, never the string "false": async was
        // assigned false, so the attribute must be absent and the property must read back false.
        Assert.Equal("lib.js|text/javascript|sha384-abc|false|false|true", OutputOf(html));
    }

    /// <summary>
    /// <c>script.src</c> reads back as an absolute URL, as the IDL getter specifies, even though the
    /// content attribute stays exactly as written.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheSrcGetterResolvesAgainstThePageUrl()
    {
        var html = Run(
            "<script>var s = document.createElement('script');" +
            "s.src = 'sub/lib.js';" +
            "document.getElementById('out').textContent = s.src;</script>");

        Assert.Equal("https://example.test/sub/lib.js", OutputOf(html));
    }

    /// <summary>
    /// A script that fails to load has to say so: a loader waiting on <c>onload</c>/<c>onerror</c>
    /// hangs forever on silence, which is the same failure mode from the other direction.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AScriptThatCannotBeLoadedFiresError()
    {
        var html = Run(
            "<script>var s = document.createElement('script');" +
            "s.src = 'file:///broiler-does-not-exist/missing.js';" +
            "s.onerror = function(){ document.getElementById('out').textContent = 'error fired'; };" +
            "document.head.appendChild(s);</script>");

        Assert.Equal("error fired", OutputOf(html));
    }
}
