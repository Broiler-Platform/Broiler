namespace Broiler.Cli.Tests;

/// <summary>
/// JavaScript is single-threaded, so a promise reaction must run on the thread driving the page, at
/// a microtask checkpoint — not on the thread pool, concurrently with it.
/// <para>
/// The engine picks a reaction's target with
/// <c>SynchronizationContext.Current ?? context.synchronizationContext</c> and falls back to
/// <c>ThreadPool.QueueUserWorkItem</c>. Nothing installed a queueing context, so <c>await</c>
/// continuations resumed on pool threads and raced the host's serialize/render. Cheap continuations
/// usually won the race and looked fine; an expensive one lost it. A geometry query — a full layout —
/// lost reliably, so <c>await something; el.getBoundingClientRect()</c> appeared to truncate the rest
/// of the function silently: no exception, no rejection, not even a <c>finally</c>, because the
/// continuation was still running elsewhere when the page was read.
/// </para>
/// </summary>
public class AsyncContinuationThreadingTests
{
    private static string Exec(string script)
    {
        var html = $@"<!doctype html>
<html><head><title>Test</title></head>
<body>
<div id=""tall"" style=""width:10px;height:2000px""></div>
<div id=""result""></div>
<script>
{script}
</script>
</body>
</html>";
        return CaptureService.ExecuteScriptsWithDom(html, "https://example.com");
    }

    [Fact]
    public void A_Geometry_Query_After_Await_Completes_Before_The_Page_Is_Read()
    {
        var result = Exec("""
            var log = [];
            async function run() {
              log.push('before');
              await Promise.resolve();
              log.push('after');
              log.push('h=' + document.documentElement.scrollHeight);
              log.push('end');
            }
            run().then(function(){ log.push('resolved'); document.getElementById('result').textContent = log.join('|'); });
        """);

        Assert.Contains("before|after|", result);
        Assert.Contains("|end|resolved", result);
        Assert.DoesNotContain("h=0|", result);
    }

    [Fact]
    public void The_Rest_Of_An_Async_Function_Runs_After_A_Geometry_Query()
    {
        // The specific shape that broke: the statements after the measurement, and the try/finally
        // around it, must all run.
        var result = Exec("""
            var log = [];
            async function run() {
              await Promise.resolve();
              try { document.getElementById('tall').getBoundingClientRect(); log.push('measured'); }
              finally { log.push('finally'); }
              log.push('tail');
              document.getElementById('result').textContent = log.join('|');
            }
            run();
        """);

        Assert.Contains("measured|finally|tail", result);
    }

    [Fact]
    public void Await_Continuations_Keep_Microtask_Ordering()
    {
        // Running reactions as microtasks on the driving thread — rather than whenever a pool thread
        // happens to get to them — also makes their ordering deterministic.
        var result = Exec("""
            var log = [];
            (async function(){ log.push('a1'); await Promise.resolve(); log.push('a2'); })();
            Promise.resolve().then(function(){ log.push('p1'); });
            log.push('sync');
            setTimeout(function(){
              log.push('timeout');
              document.getElementById('result').textContent = log.join('|');
            }, 0);
        """);

        // The synchronous body runs to completion first, then both microtasks, then the timer task.
        // The relative order of the two microtasks is an engine tick detail and deliberately not
        // pinned; that they both land in the microtask phase — rather than whenever a pool thread
        // reaches them — is the point.
        Assert.Matches(@"a1\|sync\|(a2\|p1|p1\|a2)\|timeout", result);
    }

    [Fact]
    public void A_Scroll_Awaited_Through_Its_Event_Is_Observed()
    {
        // The pattern the WPT scroll tests use: set a scroll offset, await the resulting event, then
        // measure. Every step has to land before the page is read.
        var result = Exec("""
            var log = [];
            async function run() {
              await new Promise(function(resolve){
                addEventListener('scroll', function(){ log.push('scrolled'); resolve(); }, { once: true });
                document.documentElement.scrollTop = 400;
              });
              log.push('top=' + document.documentElement.scrollTop);
              document.getElementById('result').textContent = log.join('|');
            }
            run();
        """);

        Assert.Contains("scrolled|top=400", result);
    }
}
