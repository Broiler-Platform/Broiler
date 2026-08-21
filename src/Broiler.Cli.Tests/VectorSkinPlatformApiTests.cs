namespace Broiler.Cli.Tests;

/// <summary>
/// The platform surface MediaWiki's Vector 2022 skin asks for and did not get. Each of these threw
/// out of a module the skin loads, and the skin's own <c>main()</c> never ran, so the page rendered
/// as the server sent it rather than as the skin arranges it.
/// </summary>
/// <remarks>
/// They are exercised end-to-end through the bridge, because what mattered was not that a name
/// existed but that a page could use it the way the skin does: read <c>document.readyState</c>,
/// listen for <c>readystatechange</c>, push a history entry, construct a
/// <c>PerformanceObserver</c>, and attach a listener to a <c>MediaQueryList</c>.
/// </remarks>
public sealed class VectorSkinPlatformApiTests
{
    /// <summary>
    /// HTML §3.1.7: <c>document.readyState</c> is <c>loading</c> while the parser runs. It was
    /// <c>undefined</c>, and a script that waits for `interactive` or `complete` before doing its
    /// work therefore waited forever — which is what left the appearance panel in the page column
    /// and the site notice unrendered.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Document_ReadyState_Is_Loading_During_Parse()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<script>
var el = document.createElement('div');
el.id = 'result';
el.textContent = 'state=' + document.readyState;
document.body.appendChild(el);
</script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///readystate.html");

        Assert.Contains("state=loading", result);
    }

    /// <summary>And it advances, announcing each step — the event is how a script that loads after
    /// `interactive` still learns that the document is ready.</summary>
    [Fact(Timeout = 600000)]
    public void Document_ReadyState_Advances_And_Fires_ReadyStateChange()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<div id="result"></div>
<script>
var seen = [];
document.addEventListener('readystatechange', function () { seen.push(document.readyState); });
document.addEventListener('DOMContentLoaded', function () {
  document.getElementById('result').textContent = 'seen=' + seen.join(',') + '|at-dcl=' + document.readyState;
});
</script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///readystatechange.html");

        Assert.Contains("seen=interactive", result);
        Assert.Contains("at-dcl=interactive", result);
    }

    /// <summary>
    /// The skin's client-preference code calls <c>history.replaceState</c> whenever a preference
    /// changes, and reads <c>history.state</c> back. A missing <c>history</c> object threw on the
    /// first call and took the rest of the module with it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void History_Records_And_Returns_Pushed_State()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<script>
history.pushState({ a: 1 }, '', '#one');
var pushed = history.state ? history.state.a : 'missing';
history.replaceState({ a: 2 }, '');
var replaced = history.state ? history.state.a : 'missing';
var el = document.createElement('div');
el.id = 'result';
el.textContent = 'pushed=' + pushed + '|replaced=' + replaced +
                 '|len=' + (history.length > 0) +
                 '|restore=' + history.scrollRestoration +
                 '|back=' + (typeof history.back) + '|go=' + (typeof history.go);
document.body.appendChild(el);
</script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///history.html");

        Assert.Contains("pushed=1|replaced=2|len=true|restore=auto|back=function|go=function", result);
    }

    /// <summary>
    /// <c>PerformanceObserver</c> and <c>requestIdleCallback</c> are what the skin's instrumentation
    /// and its deferred work are built on. Neither existed, and the constructor call threw before
    /// anything after it in the module ran.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void PerformanceObserver_And_RequestIdleCallback_Exist_And_Are_Usable()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<script>
var ok = 'no';
try {
  var po = new PerformanceObserver(function () {});
  po.observe({ entryTypes: ['paint'] });
  po.disconnect();
  ok = 'yes';
} catch (e) { ok = 'threw:' + e.message; }
var idle = requestIdleCallback(function () {});
cancelIdleCallback(idle);
var el = document.createElement('div');
el.id = 'result';
el.textContent = 'observer=' + ok +
                 '|supported=' + (PerformanceObserver.supportedEntryTypes.length >= 0) +
                 '|idle=' + (typeof requestIdleCallback) +
                 '|cancel=' + (typeof cancelIdleCallback);
document.body.appendChild(el);
</script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///observer.html");

        Assert.Contains("observer=yes|supported=true|idle=function|cancel=function", result);
    }

    /// <summary>
    /// CSSOM View §4.2: a <c>MediaQueryList</c> is an <c>EventTarget</c>. The skin watches its own
    /// breakpoints through one, and <c>addListener</c> alone (the deprecated spelling, which did
    /// exist) is not what current code calls.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void MediaQueryList_Is_An_EventTarget()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<script>
var mql = window.matchMedia('(min-width: 100px)');
var noop = function () {};
mql.addEventListener('change', noop);
mql.removeEventListener('change', noop);
var el = document.createElement('div');
el.id = 'result';
el.textContent = 'add=' + (typeof mql.addEventListener) +
                 '|remove=' + (typeof mql.removeEventListener) +
                 '|dispatch=' + (typeof mql.dispatchEvent) +
                 '|onchange=' + (mql.onchange === null ? 'null' : typeof mql.onchange);
document.body.appendChild(el);
</script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///mql.html");

        Assert.Contains("add=function|remove=function|dispatch=function|onchange=null", result);
    }
}
