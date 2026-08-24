using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>fetch()</c> and the body-consuming methods return real, chainable Promises rather than the
/// hand-rolled thenables they used to.
/// </summary>
/// <remarks>
/// Both thenables carried a <c>then</c> that invoked the callback and returned <b>themselves</b>, and
/// that is the defect worth naming: <c>.then(a).then(b)</c> ran <c>b</c> against the ORIGINAL value
/// instead of <c>a</c>'s result, so the ordinary <c>fetch(u).then(r =&gt; r.json()).then(useData)</c>
/// shape handed the second callback the Response rather than the parsed body — a silently wrong
/// value, not an error. Alongside it: <c>.then</c>'s onRejected argument was ignored, <c>.finally</c>
/// did not exist, a callback that threw was logged and swallowed instead of rejecting the derived
/// promise, and the object was not <c>instanceof Promise</c>.
/// </remarks>
public class FetchPromiseConformanceTests
{
    private static string ExecJs(string jsCode)
    {
        var html = $@"<!doctype html>
<html><head><title>T</title></head><body><div id=""result""></div>
<script>
{jsCode}
</script>
</body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "https://example.com/page");
    }

    [Fact(Timeout = 600000)]
    public void Fetch_Returns_A_Real_Promise_With_The_Full_Prototype()
    {
        var result = ExecJs(@"
            var p = fetch('/x');
            document.getElementById('result').textContent = 'V:' + (p instanceof Promise) +
                ',' + typeof p.then + ',' + typeof p.catch + ',' + typeof p.finally;
        ");
        Assert.Contains("V:true,function,function,function", result);
    }

    // The silent wrong-value bug: the second callback used to receive the Response, not 'MAPPED'.
    [Fact(Timeout = 600000)]
    public void Chaining_Passes_The_Previous_Callbacks_Result_Not_The_Original_Value()
    {
        var result = ExecJs(@"
            var o = document.getElementById('result');
            o.textContent = 'V:NOTRUN';
            fetch('/x').then(function (r) { return 'MAPPED'; })
                       .then(function (v) { o.textContent = 'V:' + v; });
        ");
        Assert.Contains("V:MAPPED", result);
    }

    // The canonical shape, which is the same bug one level deeper: r.text() returns a promise, and the
    // next .then must receive the body string rather than the Response.
    [Fact(Timeout = 600000)]
    public void The_Canonical_Fetch_Then_Body_Then_Use_Chain_Delivers_The_Body()
    {
        var result = ExecJs(@"
            var o = document.getElementById('result');
            o.textContent = 'V:NOTRUN';
            fetch('/x').then(function (r) { return r.text(); })
                       .then(function (t) { o.textContent = 'V:' + typeof t; });
        ");
        Assert.Contains("V:string", result);
    }

    // Body promises chain among themselves too.
    [Fact(Timeout = 600000)]
    public void A_Body_Promise_Chains_Through_Its_Callbacks_Result()
    {
        var result = ExecJs(@"
            var o = document.getElementById('result');
            o.textContent = 'V:NOTRUN';
            new Response('hello').text()
                .then(function (t) { return t.toUpperCase(); })
                .then(function (v) { o.textContent = 'V:' + v; });
        ");
        Assert.Contains("V:HELLO", result);
    }

    // .finally did not exist at all, so calling it was a TypeError.
    [Fact(Timeout = 600000)]
    public void Finally_Runs()
    {
        var result = ExecJs(@"
            var o = document.getElementById('result');
            o.textContent = 'V:NOTRUN';
            fetch('/x').finally(function () { o.textContent = 'V:FIN'; });
        ");
        Assert.Contains("V:FIN", result);
    }

    [Fact(Timeout = 600000)]
    public void Await_Resolves_To_The_Response()
    {
        var result = ExecJs(@"
            var o = document.getElementById('result');
            o.textContent = 'V:NOTRUN';
            (async function () { var r = await fetch('/x'); o.textContent = 'V:' + typeof r.status; })();
        ");
        Assert.Contains("V:number", result);
    }

    // A throw inside a handler used to be caught and logged; it must reject the derived promise.
    [Fact(Timeout = 600000)]
    public void A_Throwing_Handler_Rejects_The_Derived_Promise()
    {
        var result = ExecJs(@"
            var o = document.getElementById('result');
            o.textContent = 'V:NOTRUN';
            fetch('/x').then(function () { throw new Error('boom'); })
                       .catch(function (e) { o.textContent = 'V:CAUGHT ' + e.message; });
        ");
        Assert.Contains("V:CAUGHT boom", result);
    }

    // .then's second argument was ignored entirely.
    [Fact(Timeout = 600000)]
    public void Then_Honours_Its_OnRejected_Argument()
    {
        var result = ExecJs(@"
            var o = document.getElementById('result');
            o.textContent = 'V:NOTRUN';
            fetch('/x').then(function () { throw new Error('x'); })
                       .then(function () { o.textContent = 'V:WRONG'; },
                             function (e) { o.textContent = 'V:ONREJECTED'; });
        ");
        Assert.Contains("V:ONREJECTED", result);
    }

    // A resolver that throws — .json() over a malformed body — rejects instead of throwing
    // synchronously out of .then.
    [Fact(Timeout = 600000)]
    public void A_Body_Resolver_That_Throws_Rejects_Rather_Than_Throwing_Synchronously()
    {
        var result = ExecJs(@"
            var o = document.getElementById('result');
            o.textContent = 'V:NOTRUN';
            new Response('not json').json()
                .then(function () { o.textContent = 'V:RESOLVED'; })
                .catch(function () { o.textContent = 'V:REJECTED'; });
        ");
        Assert.Contains("V:REJECTED", result);
    }
}
