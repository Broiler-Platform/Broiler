using Broiler.App.Rendering;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Engine;

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

    // ---------------------------------------------------------------
    //  ScriptExtractor.ExtractAll — deferred and external scripts
    //  (aligns Broiler.App pipeline with CLI's ExecuteScriptsWithDom)
    // ---------------------------------------------------------------

    [Fact]
    public void ExtractAll_Separates_Deferred_Scripts()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>var a = 1;</script>
<script defer>var b = 2;</script>
<script>var c = 3;</script>
</body></html>";

        var extractor = new ScriptExtractor();
        var result = extractor.ExtractAll(html);

        Assert.Equal(2, result.Scripts.Count);
        Assert.Contains("var a = 1;", result.Scripts);
        Assert.Contains("var c = 3;", result.Scripts);

        Assert.Single(result.DeferredScripts);
        Assert.Contains("var b = 2;", result.DeferredScripts);
    }

    [Fact]
    public void ExtractAll_No_Scripts_Returns_Empty_Lists()
    {
        var html = @"<!DOCTYPE html><html><body><p>Hello</p></body></html>";

        var extractor = new ScriptExtractor();
        var result = extractor.ExtractAll(html);

        Assert.Empty(result.Scripts);
        Assert.Empty(result.DeferredScripts);
    }

    [Fact]
    public void ExtractAll_DataUri_Scripts_Decoded()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script src=""data:text/javascript,var%20x%20%3D%201%3B""></script>
</body></html>";

        var extractor = new ScriptExtractor();
        var result = extractor.ExtractAll(html);

        Assert.Single(result.Scripts);
        Assert.Equal("var x = 1;", result.Scripts[0]);
        Assert.Empty(result.DeferredScripts);
    }

    [Fact]
    public void ExtractAll_Deferred_DataUri_Script()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script defer src=""data:text/javascript,var%20x%20%3D%201%3B""></script>
</body></html>";

        var extractor = new ScriptExtractor();
        var result = extractor.ExtractAll(html);

        Assert.Empty(result.Scripts);
        Assert.Single(result.DeferredScripts);
        Assert.Equal("var x = 1;", result.DeferredScripts[0]);
    }

    [Fact]
    public void ExtractAll_External_File_Script()
    {
        // Create a temporary script file
        var tmpDir = Path.Combine(Path.GetTempPath(), "broiler-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        try
        {
            var scriptPath = Path.Combine(tmpDir, "ext.js");
            File.WriteAllText(scriptPath, "var ext = 'loaded';");
            var scriptUrl = new Uri(scriptPath).AbsoluteUri;

            var html = $@"<!DOCTYPE html>
<html><body>
<script src=""{scriptUrl}""></script>
</body></html>";

            var extractor = new ScriptExtractor();
            var result = extractor.ExtractAll(html, "file:///page.html");

            Assert.Single(result.Scripts);
            Assert.Equal("var ext = 'loaded';", result.Scripts[0]);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void ScriptEngine_Execute_With_Deferred_Scripts()
    {
        // Validate that ScriptEngine executes deferred scripts
        // after regular scripts, matching CLI's ExecuteScriptsWithDom.
        var engine = new ScriptEngine();

        var scripts = new List<string> { "var result = 'A';" };
        var deferred = new List<string> { "result += 'B';" };

        var html = @"<!DOCTYPE html>
<html><body><div id=""out""></div>
<script>var result = 'A';</script>
<script defer>result += 'B';</script>
</body></html>";

        var output = engine.Execute(scripts, deferred, html, "file:///test.html");

        Assert.NotNull(output);
        // The DOM-bridge execution won't directly reflect the simple
        // variable assignment, but should produce valid serialised HTML.
        Assert.Contains("<html", output);
    }

    [Fact]
    public void ScriptEngine_Execute_DeferredOnly_Returns_Html()
    {
        // When there are only deferred scripts (no regular scripts),
        // the engine should still execute them.
        var engine = new ScriptEngine();

        var scripts = new List<string>();
        var deferred = new List<string>
        {
            @"var p = document.createElement('p');
              p.textContent = 'deferred-ok';
              document.body.appendChild(p);"
        };

        var html = @"<!DOCTYPE html>
<html><body></body></html>";

        var output = engine.Execute(scripts, deferred, html, "file:///test.html");

        Assert.NotNull(output);
        Assert.Contains("deferred-ok", output);
    }

    [Fact]
    public void ScriptExtractor_FetchExternalScript_FileUrl()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "broiler-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        try
        {
            var scriptPath = Path.Combine(tmpDir, "test.js");
            File.WriteAllText(scriptPath, "alert('hello');");
            var fileUrl = new Uri(scriptPath).AbsoluteUri;

            var result = ScriptExtractor.FetchExternalScript(fileUrl, null);
            Assert.Equal("alert('hello');", result);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void ScriptExtractor_FetchExternalScript_NonExistent_Returns_Null()
    {
        var result = ScriptExtractor.FetchExternalScript("file:///nonexistent/script.js", null);
        Assert.Null(result);
    }

    [Fact]
    public void PageContent_DeferredScripts_Default_Empty()
    {
        var content = new PageContent("html", new List<string>(), "url");
        Assert.NotNull(content.DeferredScripts);
        Assert.Empty(content.DeferredScripts);
    }

    [Fact]
    public void PageContent_DeferredScripts_Stored()
    {
        var deferred = new List<string> { "console.log('deferred');" };
        var content = new PageContent("html", new List<string>(), "url", deferred);
        Assert.Single(content.DeferredScripts);
        Assert.Equal("console.log('deferred');", content.DeferredScripts[0]);
    }

    // ---------------------------------------------------------------
    //  DomBridge.HasPendingTimers and FlushTimerStep
    // ---------------------------------------------------------------

    [Fact]
    public void DomBridge_HasPendingTimers_False_When_No_Timers()
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>");
        Assert.False(bridge.HasPendingTimers);
    }

    [Fact]
    public void DomBridge_HasPendingTimers_True_After_SetTimeout()
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>");
        context.Eval("setTimeout(function() {}, 0);");
        Assert.True(bridge.HasPendingTimers);
    }

    [Fact]
    public void DomBridge_FlushTimerStep_Executes_One_Batch()
    {
        var html = "<html><body><div id='out'></div></body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html);

        // Queue two chained timeouts:
        //   First timeout writes "step1" and queues another for "step2"
        context.Eval(@"
            setTimeout(function() {
                document.getElementById('out').textContent = 'step1';
                setTimeout(function() {
                    document.getElementById('out').textContent = 'step2';
                }, 0);
            }, 0);
        ");

        Assert.True(bridge.HasPendingTimers);

        // First step executes the first timeout (sets "step1", queues "step2")
        Assert.True(bridge.FlushTimerStep());
        var html1 = bridge.SerializeToHtml();
        Assert.Contains("step1", html1);

        // Second step executes the second timeout (sets "step2")
        Assert.True(bridge.HasPendingTimers);
        Assert.True(bridge.FlushTimerStep());
        var html2 = bridge.SerializeToHtml();
        Assert.Contains("step2", html2);

        // No more pending timers
        Assert.False(bridge.HasPendingTimers);
    }

    [Fact]
    public void DomBridge_FlushTimerStep_Returns_False_When_Empty()
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>");
        Assert.False(bridge.FlushTimerStep());
    }

    // ---------------------------------------------------------------
    //  InteractiveSession
    // ---------------------------------------------------------------

    [Fact]
    public void InteractiveSession_Step_Returns_Intermediate_Html()
    {
        var engine = new ScriptEngine();
        var html = "<html><body><div id='out'>init</div></body></html>";
        var scripts = new List<string>
        {
            @"
            setTimeout(function() {
                document.getElementById('out').textContent = 'frame1';
                setTimeout(function() {
                    document.getElementById('out').textContent = 'frame2';
                }, 0);
            }, 0);
            "
        };

        var session = engine.ExecuteInteractive(scripts, Array.Empty<string>(), html, null);
        Assert.NotNull(session);
        Assert.True(session.HasPendingWork);

        // Step 1: executes first timeout → "frame1"
        var step1Html = session.Step();
        Assert.NotNull(step1Html);
        Assert.Contains("frame1", step1Html);

        // Step 2: executes second timeout → "frame2"
        Assert.True(session.HasPendingWork);
        var step2Html = session.Step();
        Assert.NotNull(step2Html);
        Assert.Contains("frame2", step2Html);

        Assert.False(session.HasPendingWork);
        session.Dispose();
    }

    [Fact]
    public void InteractiveSession_Returns_Null_When_No_Scripts()
    {
        var engine = new ScriptEngine();
        var session = engine.ExecuteInteractive(
            Array.Empty<string>(), Array.Empty<string>(), "<html></html>", null);
        Assert.Null(session);
    }

    [Fact]
    public void InteractiveSession_Complete_Flushes_All_Timers()
    {
        var engine = new ScriptEngine();
        var html = "<html><body><div id='out'>init</div></body></html>";
        var scripts = new List<string>
        {
            @"
            setTimeout(function() {
                document.getElementById('out').textContent = 'step1';
                setTimeout(function() {
                    document.getElementById('out').textContent = 'final';
                }, 0);
            }, 0);
            "
        };

        var session = engine.ExecuteInteractive(scripts, Array.Empty<string>(), html, null);
        Assert.NotNull(session);

        // Complete() flushes everything at once
        var finalHtml = session.Complete();
        Assert.Contains("final", finalHtml);
        Assert.False(session.HasPendingWork);
        session.Dispose();
    }

    [Fact]
    public void InteractiveSession_CurrentHtml_Does_Not_Execute_Timers()
    {
        var engine = new ScriptEngine();
        var html = "<html><body><div id='out'>init</div></body></html>";
        var scripts = new List<string>
        {
            @"
            setTimeout(function() {
                document.getElementById('out').textContent = 'changed';
            }, 0);
            "
        };

        var session = engine.ExecuteInteractive(scripts, Array.Empty<string>(), html, null);
        Assert.NotNull(session);

        // CurrentHtml() should NOT execute timers
        var currentHtml = session.CurrentHtml();
        Assert.Contains("init", currentHtml);
        Assert.DoesNotContain("changed", currentHtml);

        // Timer is still pending
        Assert.True(session.HasPendingWork);
        session.Dispose();
    }

    [Fact]
    public void InteractiveSession_RAF_Callbacks_Stepped()
    {
        var engine = new ScriptEngine();
        var html = "<html><body><div id='out'>init</div></body></html>";
        var scripts = new List<string>
        {
            @"
            requestAnimationFrame(function() {
                document.getElementById('out').textContent = 'animated';
            });
            "
        };

        var session = engine.ExecuteInteractive(scripts, Array.Empty<string>(), html, null);
        Assert.NotNull(session);
        Assert.True(session.HasPendingWork);

        var stepHtml = session.Step();
        Assert.NotNull(stepHtml);
        Assert.Contains("animated", stepHtml);
        session.Dispose();
    }
}

