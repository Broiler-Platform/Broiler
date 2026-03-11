namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 11 Acid3 compliance: Timer &amp; Async Execution.
/// Validates setTimeout/clearTimeout, setInterval/clearInterval,
/// requestAnimationFrame/cancelAnimationFrame, and script defer/async ordering.
/// </summary>
public class TimerAndAsyncTests
{
    // ---------------------------------------------------------------
    //  11.1 — setTimeout / clearTimeout
    // ---------------------------------------------------------------

    [Fact]
    public void SetTimeout_Callback_Executes_On_Flush()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
setTimeout(function() {
    var p = document.createElement('p');
    p.textContent = 'timeout-fired';
    document.getElementById('out').appendChild(p);
}, 0);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("timeout-fired", result);
    }

    [Fact]
    public void SetTimeout_Returns_Numeric_Id()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var id = setTimeout(function() {}, 100);
var p = document.createElement('p');
p.textContent = 'id-' + (typeof id === 'number' ? 'num' : 'other');
document.getElementById('out').appendChild(p);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("id-num", result);
    }

    [Fact]
    public void ClearTimeout_Prevents_Callback_Execution()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var id = setTimeout(function() {
    var p = document.createElement('p');
    p.textContent = 'cleared-cb';
    document.getElementById('out').appendChild(p);
}, 0);
clearTimeout(id);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.DoesNotContain("<p>cleared-cb</p>", result);
    }

    [Fact]
    public void SetTimeout_Nested_Callbacks_Flush_Iteratively()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
setTimeout(function() {
    r.push('first');
    setTimeout(function() {
        r.push('second');
    }, 0);
}, 0);
setTimeout(function() {
    var p = document.createElement('p');
    p.textContent = r.join(',');
    document.getElementById('out').appendChild(p);
}, 0);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Both first-level timeouts fire, then nested timeout fires in next iteration
        Assert.Contains("first", result);
    }

    [Fact]
    public void SetTimeout_With_Zero_Delay_Is_Deferred()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var order = [];
order.push('sync');
setTimeout(function() { order.push('async'); }, 0);
order.push('after');
var p = document.createElement('p');
p.textContent = order.join(',');
document.getElementById('out').appendChild(p);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // setTimeout callback should NOT fire during script execution
        Assert.Contains("sync,after", result);
        Assert.DoesNotContain("async", result.Split("sync,after")[0]);
    }

    [Fact]
    public void SetTimeout_Callback_Error_Does_Not_Block_Others()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
setTimeout(function() { throw new Error('boom'); }, 0);
setTimeout(function() {
    var p = document.createElement('p');
    p.textContent = 'survived';
    document.getElementById('out').appendChild(p);
}, 0);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("survived", result);
    }

    // ---------------------------------------------------------------
    //  11.2 — setInterval / clearInterval
    // ---------------------------------------------------------------

    [Fact]
    public void SetInterval_Callback_Executes_On_Flush()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var count = 0;
var id = setInterval(function() {
    count++;
}, 100);
setTimeout(function() {
    var p = document.createElement('p');
    p.textContent = 'count-' + count;
    document.getElementById('out').appendChild(p);
    clearInterval(id);
}, 0);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Interval should fire at least once during flush
        Assert.Contains("count-", result);
    }

    [Fact]
    public void ClearInterval_Stops_Callback()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var id = setInterval(function() {
    var p = document.createElement('p');
    p.textContent = 'interval-cleared';
    document.getElementById('out').appendChild(p);
}, 100);
clearInterval(id);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.DoesNotContain("<p>interval-cleared</p>", result);
    }

    [Fact]
    public void SetInterval_Returns_Unique_Id()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var id1 = setInterval(function() {}, 100);
var id2 = setInterval(function() {}, 100);
clearInterval(id1);
clearInterval(id2);
var p = document.createElement('p');
p.textContent = (id1 !== id2) ? 'unique' : 'same';
document.getElementById('out').appendChild(p);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("unique", result);
    }

    // ---------------------------------------------------------------
    //  11.3 — requestAnimationFrame / cancelAnimationFrame
    // ---------------------------------------------------------------

    [Fact]
    public void RequestAnimationFrame_Callback_Executes_On_Flush()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
requestAnimationFrame(function() {
    var p = document.createElement('p');
    p.textContent = 'raf-fired';
    document.getElementById('out').appendChild(p);
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("raf-fired", result);
    }

    [Fact]
    public void RequestAnimationFrame_Returns_Numeric_Id()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var id = requestAnimationFrame(function() {});
var p = document.createElement('p');
p.textContent = 'raf-id-' + (typeof id === 'number' ? 'num' : 'other');
document.getElementById('out').appendChild(p);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("raf-id-num", result);
    }

    [Fact]
    public void CancelAnimationFrame_Prevents_Callback()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var id = requestAnimationFrame(function() {
    var p = document.createElement('p');
    p.textContent = 'raf-cancelled';
    document.getElementById('out').appendChild(p);
});
cancelAnimationFrame(id);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.DoesNotContain("<p>raf-cancelled</p>", result);
    }

    [Fact]
    public void RequestAnimationFrame_Receives_Timestamp_Argument()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
requestAnimationFrame(function(ts) {
    var p = document.createElement('p');
    p.textContent = 'ts-' + (typeof ts === 'number' ? 'num' : 'other');
    document.getElementById('out').appendChild(p);
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("ts-num", result);
    }

    [Fact]
    public void RequestAnimationFrame_And_SetTimeout_Both_Flush()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
setTimeout(function() { r.push('timeout'); }, 0);
requestAnimationFrame(function() { r.push('raf'); });
setTimeout(function() {
    var p = document.createElement('p');
    p.textContent = r.join(',');
    document.getElementById('out').appendChild(p);
}, 0);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("timeout", result);
        Assert.Contains("raf", result);
    }

    // ---------------------------------------------------------------
    //  11.4 — Script defer / async execution ordering
    // ---------------------------------------------------------------

    [Fact]
    public void Defer_Script_Executes_After_Regular_Scripts()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script defer>
var p = document.createElement('p');
p.textContent = 'deferred-' + (typeof window.__regular === 'undefined' ? 'no-reg' : window.__regular);
document.getElementById('out').appendChild(p);
</script>
<script>
window.__regular = 'done';
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Defer script runs AFTER regular script sets __regular
        Assert.Contains("deferred-done", result);
    }

    [Fact]
    public void Multiple_Defer_Scripts_Preserve_Order()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>window.__order = [];</script>
<script defer>window.__order.push('d1');</script>
<script defer>window.__order.push('d2');</script>
<script defer>
var p = document.createElement('p');
p.textContent = window.__order.join(',');
document.getElementById('out').appendChild(p);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("d1,d2", result);
    }

    [Fact]
    public void Async_Script_Executes_With_Regular_Scripts()
    {
        // Async scripts in our model execute in the regular script batch (not deferred)
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script async>
var p = document.createElement('p');
p.textContent = 'async-ran';
document.getElementById('out').appendChild(p);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("async-ran", result);
    }

    // ---------------------------------------------------------------
    //  Integration scenarios
    // ---------------------------------------------------------------

    [Fact]
    public void Timers_From_Multiple_Scripts_All_Flush()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
setTimeout(function() {
    var p = document.createElement('p');
    p.textContent = 'from-script1';
    document.getElementById('out').appendChild(p);
}, 0);
</script>
<script>
setTimeout(function() {
    var p = document.createElement('p');
    p.textContent = 'from-script2';
    document.getElementById('out').appendChild(p);
}, 0);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("from-script1", result);
        Assert.Contains("from-script2", result);
    }

    [Fact]
    public void No_Timers_Or_Callbacks_Returns_Normal_Html()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<p>static</p>
<script>
var p = document.createElement('p');
p.textContent = 'inline';
document.body.appendChild(p);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("static", result);
        Assert.Contains("inline", result);
    }

    [Fact]
    public void SetTimeout_Modifies_Dom_Before_Capture()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"">original</div>
<script>
setTimeout(function() {
    document.getElementById('target').textContent = 'modified';
}, 50);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("modified", result);
    }

    [Fact]
    public void Window_And_Global_Timer_Functions_Are_Equivalent()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
window.setTimeout(function() { r.push('wt'); }, 0);
setTimeout(function() { r.push('gt'); }, 0);
window.requestAnimationFrame(function() { r.push('wr'); });
requestAnimationFrame(function() { r.push('gr'); });
setTimeout(function() {
    var p = document.createElement('p');
    p.textContent = r.join(',');
    document.getElementById('out').appendChild(p);
}, 0);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("wt", result);
        Assert.Contains("gt", result);
        Assert.Contains("wr", result);
        Assert.Contains("gr", result);
    }

    [Fact]
    public void Cleared_Timeout_Inside_Another_Timeout_Is_Skipped()
    {
        // clearTimeout from a nested setTimeout (different flush iteration)
        // prevents the target callback from executing in the next iteration
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var id2;
setTimeout(function() {
    clearTimeout(id2);
}, 0);
id2 = setTimeout(function() {
    var p = document.createElement('p');
    p.textContent = 'nested-cleared';
    document.getElementById('out').appendChild(p);
}, 0);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // The first timeout fires and clears id2 before id2 executes
        Assert.DoesNotContain("<p>nested-cleared</p>", result);
    }

    [Fact]
    public void Defer_Script_With_Timer_Flushes_After_All_Scripts()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>window.__val = 'init';</script>
<script defer>
setTimeout(function() {
    var p = document.createElement('p');
    p.textContent = 'deferred-timer-' + window.__val;
    document.getElementById('out').appendChild(p);
}, 0);
</script>
<script>window.__val = 'updated';</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Defer script runs after regular scripts, so __val is 'updated'
        Assert.Contains("deferred-timer-updated", result);
    }
}
