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
    /// Background Tasks §2.3 hands the callback an <c>IdleDeadline</c>. <c>requestIdleCallback</c>
    /// was a bare alias of <c>setTimeout</c>, which invokes its callback with no arguments at all, so
    /// the parameter was <c>undefined</c> and the first thing every caller does with it —
    /// <c>deadline.timeRemaining()</c> — threw "Cannot get property timeRemaining of undefined".
    /// mediawiki.org reported it twice per load.
    /// </summary>
    [Fact]
    public void An_Idle_Callback_Is_Handed_A_Deadline_It_Can_Read()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<div id="result"></div>
<script>
requestIdleCallback(function (deadline) {
  var text;
  try {
    text = 'type=' + typeof deadline.timeRemaining +
           '|budget=' + (deadline.timeRemaining() > 0 && deadline.timeRemaining() <= 50) +
           '|didTimeout=' + deadline.didTimeout;
  } catch (e) {
    text = 'threw=' + e.message;
  }
  document.getElementById('result').textContent = text;
});
</script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///idle.html");

        Assert.Contains(">type=function|budget=true|didTimeout=false<", result);
    }

    /// <summary>
    /// The shape the deadline actually exists for, and the one MediaWiki runs its module evaluation
    /// through: a loop that drains a queue while the budget lasts and reschedules itself when it does
    /// not. It has to both make progress and terminate — a <c>timeRemaining()</c> stuck at 0 would
    /// reschedule for ever without shifting a single item.
    /// </summary>
    [Fact]
    public void A_Queue_Draining_Idle_Loop_Makes_Progress_And_Terminates()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<div id="result"></div>
<script>
var queue = [1, 2, 3, 4, 5, 6, 7, 8], done = [], rounds = 0;
function drain(deadline) {
  rounds++;
  while (queue.length && deadline.timeRemaining() > 3) {
    done.push(queue.shift());
  }
  if (queue.length) {
    requestIdleCallback(drain);
  } else {
    document.getElementById('result').textContent =
        'done=' + done.join(',') + '|left=' + queue.length + '|rounded=' + (rounds >= 1);
  }
}
requestIdleCallback(drain);
</script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///idle-drain.html");

        Assert.Contains(">done=1,2,3,4,5,6,7,8|left=0|rounded=true<", result);
    }

    /// <summary>
    /// The second argument is an options dictionary, not a delay. Routed through the
    /// <c>setTimeout</c> adapter it was read as a number, came out <c>NaN</c> and was clamped to 0,
    /// so a page's timeout was silently discarded. A callback that carries one is running because
    /// that deadline arrived — there is no idle period here it could have been run in earlier — which
    /// is what <c>didTimeout</c> reports.
    /// </summary>
    [Fact]
    public void An_Idle_Callback_Given_A_Timeout_Reports_That_It_Timed_Out()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<div id="result"></div>
<script>
requestIdleCallback(function (deadline) {
  document.getElementById('result').textContent = 'didTimeout=' + deadline.didTimeout;
}, { timeout: 50 });
</script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///idle-timeout.html");

        Assert.Contains(">didTimeout=true<", result);
    }

    /// <summary>
    /// The handle is cancellable. It comes out of the same id space <c>clearTimeout</c> acts on, so a
    /// cancelled callback has to stay uncalled rather than run anyway.
    /// </summary>
    [Fact]
    public void A_Cancelled_Idle_Callback_Does_Not_Run()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<div id="result">not-run</div>
<script>
var handle = requestIdleCallback(function () {
  document.getElementById('result').textContent = 'ran';
});
cancelIdleCallback(handle);
</script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///idle-cancel.html");

        Assert.Contains(">not-run<", result);
        Assert.DoesNotContain(">ran<", result);
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
