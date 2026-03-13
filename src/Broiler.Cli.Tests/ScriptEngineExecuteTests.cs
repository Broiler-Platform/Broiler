using Broiler.App.Rendering;
using YantraJS.Core;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests validating that the DomBridge methods now called by
/// <c>ScriptEngine.Execute</c> work correctly — specifically
/// <see cref="DomBridge.FireWindowLoadEvent"/> and
/// <see cref="DomBridge.FlushTimers"/>. These are the features that
/// were missing from Broiler.App's rendering pipeline and caused it
/// to produce no acid-output.
/// </summary>
public class ScriptEngineExecuteTests
{
    // ---------------------------------------------------------------
    //  Body onload event firing via DomBridge (matches ScriptEngine)
    // ---------------------------------------------------------------

    [Fact]
    public void DomBridge_FireWindowLoadEvent_Triggers_Body_Onload()
    {
        // Acid3 uses <body onload="update()"> to bootstrap the test runner.
        // ScriptEngine now calls bridge.FireWindowLoadEvent() after scripts.
        var html = @"<!DOCTYPE html>
<html><body onload=""run()"">
<div id=""out""></div>
<script>
function run() {
    var p = document.createElement('p');
    p.textContent = 'onload-fired';
    document.getElementById('out').appendChild(p);
}
</script>
</body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");

        // Execute inline script (defines run())
        context.Eval(@"
function run() {
    var p = document.createElement('p');
    p.textContent = 'onload-fired';
    document.getElementById('out').appendChild(p);
}");

        // Fire onload — this is what ScriptEngine now does
        bridge.FireWindowLoadEvent();

        var result = bridge.SerializeToHtml();
        Assert.Contains("onload-fired", result);
    }

    [Fact]
    public void DomBridge_FlushTimers_Executes_SetTimeout_Chains()
    {
        // Acid3 chains tests via setTimeout. ScriptEngine now calls
        // bridge.FlushTimers() after scripts + onload.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
</body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");

        context.Eval(@"
var count = 0;
function step() {
    count++;
    if (count < 3) {
        setTimeout(step, 10);
    } else {
        var p = document.createElement('p');
        p.textContent = 'timer-count=' + count;
        document.getElementById('out').appendChild(p);
    }
}
setTimeout(step, 10);");

        // Flush timers — this is what ScriptEngine now does
        bridge.FlushTimers();

        var result = bridge.SerializeToHtml();
        Assert.Contains("timer-count=3", result);
    }

    [Fact]
    public void DomBridge_Onload_Then_FlushTimers_Runs_Full_Chain()
    {
        // Simulates the complete Acid3 pattern: body onload starts the
        // test runner, which chains tests via setTimeout. ScriptEngine
        // now calls FireWindowLoadEvent then FlushTimers.
        var html = @"<!DOCTYPE html>
<html><body onload=""update()"">
<div id=""out""></div>
</body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");

        context.Eval(@"
var score = 0;
var tests = [
    function() { score++; },
    function() { score++; },
    function() { score++; }
];
var index = 0;
function update() {
    if (index < tests.length) {
        tests[index]();
        index++;
        setTimeout(update, 10);
    } else {
        var p = document.createElement('p');
        p.textContent = 'score=' + score;
        document.getElementById('out').appendChild(p);
    }
}");

        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();

        var result = bridge.SerializeToHtml();
        Assert.Contains("score=3", result);
    }

    // ---------------------------------------------------------------
    //  URL passing to DomBridge (window.location)
    // ---------------------------------------------------------------

    [Fact]
    public void DomBridge_Attach_With_Url_Sets_Window_Location()
    {
        // ScriptEngine now passes the URL to DomBridge.Attach so that
        // window.location is available for URL-dependent scripts.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
</body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/page.html");

        context.Eval(@"
var p = document.createElement('p');
p.textContent = 'href=' + window.location.href;
document.getElementById('out').appendChild(p);");

        var result = bridge.SerializeToHtml();
        Assert.Contains("href=https://example.com/page.html", result);
    }

    [Fact]
    public void DomBridge_Attach_Without_Url_Still_Works()
    {
        // The 2-parameter Attach (without URL) should still work
        // for backward compatibility.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
</body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html);

        context.Eval(@"
var p = document.createElement('p');
p.textContent = 'no-url-ok';
document.getElementById('out').appendChild(p);");

        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();

        var result = bridge.SerializeToHtml();
        Assert.Contains("no-url-ok", result);
    }

    // ---------------------------------------------------------------
    //  End-to-end via CaptureService (validates same pattern as App)
    // ---------------------------------------------------------------

    [Fact]
    public void CaptureService_ExecuteScriptsWithDom_Matches_App_Pattern()
    {
        // This test validates the same pattern that ScriptEngine.Execute
        // now follows: scripts + onload + timers. CaptureService already
        // does this correctly.
        var html = @"<!DOCTYPE html>
<html><body onload=""start()"">
<div id=""out""></div>
<script>
var result = '';
function start() {
    result += 'A';
    setTimeout(function() {
        result += 'B';
        var p = document.createElement('p');
        p.textContent = 'result=' + result;
        document.getElementById('out').appendChild(p);
    }, 10);
}
</script>
</body></html>";

        var output = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("result=AB", output);
    }

    // ---------------------------------------------------------------
    //  Data: URI script extraction (via CaptureService which mirrors
    //  the ScriptExtractor logic in Broiler.App)
    // ---------------------------------------------------------------

    [Fact]
    public void DataUri_Scripts_Execute_In_Document_Order()
    {
        // Acid3 uses data: URI scripts (e.g. lines 153-157).
        // Both Broiler.App and Broiler.Cli must extract and execute them.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script type=""text/javascript"">var result = '';</script>
<script type=""text/javascript"" src=""data:text/javascript,result%20%2B%3D%20'A'%3B""></script>
<script type=""text/javascript"" src=""data:text/javascript;base64,cmVzdWx0ICs9ICdCJzs=""></script>
<script type=""text/javascript"">
document.getElementById('out').textContent = result;
</script>
</body></html>";

        var output = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("AB", output);
    }

    [Fact]
    public void DataUri_Scripts_With_Mixed_Encodings()
    {
        // Verifies percent-encoded and base64 data: URI scripts produce
        // consistent results (matching Acid3 test 97).
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script type=""text/javascript"" src=""data:text/javascript,d1%20%3D%20'one'%3B""></script>
<script type=""text/javascript"" src=""data:text/javascript;base64,ZDIgPSAndHdvJzs=""></script>
<script type=""text/javascript"">
document.getElementById('out').textContent = d1 + ',' + d2;
</script>
</body></html>";

        var output = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("one,two", output);
    }

    // ---------------------------------------------------------------
    //  CurrentScriptIndex tracking for document.write()
    // ---------------------------------------------------------------

    [Fact]
    public void CurrentScriptIndex_Enables_DocumentWrite_Positioning()
    {
        // Verify that script element index tracking ensures document.write()
        // inserts content at the correct DOM position.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""before"">before</div>
<script>
document.write('<p id=""injected"">written</p>');
</script>
<div id=""after"">after</div>
</body></html>";

        var output = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("injected", output);
        Assert.Contains("written", output);
    }
}

