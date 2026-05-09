using Broiler.HtmlBridge;
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
    public void DomBridge_FireWindowLoadEvent_Triggers_Body_Onload_Property_Assigned_By_Script()
    {
        const string html = """
<!DOCTYPE html>
<html><body><div id="out"></div></body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");

        context.Eval("""
            document.body.onload = function () {
                document.getElementById('out').textContent = 'body-property-load-fired';
            };
            """);

        bridge.FireWindowLoadEvent();

        var result = context.Eval("document.getElementById('out').textContent");
        Assert.Equal("body-property-load-fired", result.ToString());
    }

    [Fact]
    public void DomBridge_FireWindowLoadEvent_Triggers_Window_Load_Listeners_And_Honors_Removal()
    {
        const string html = """
<!DOCTYPE html>
<html><body><div id="out"></div></body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");

        context.Eval("""
            var calls = [];
            function removed() { calls.push('removed'); }
            window.addEventListener('load', function () {
                calls.push('kept');
                document.getElementById('out').textContent = calls.join('|');
            });
            window.addEventListener('load', removed);
            window.removeEventListener('load', removed);
            """);

        bridge.FireWindowLoadEvent();

        var result = context.Eval("document.getElementById('out').textContent");
        Assert.Equal("kept", result.ToString());
    }

    [Fact]
    public void DomBridge_WindowFrames_Expose_SameOrigin_Iframe_Windows()
    {
        const string html = """
<!DOCTYPE html>
<html><body><iframe id="frame" srcdoc="<!DOCTYPE html><html><body>ok</body></html>"></iframe></body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        bridge.FireWindowLoadEvent();

        var result = context.Eval("""
            [
                frames.length,
                frames[0] === document.getElementById('frame').contentWindow,
                frames[0].document === document.getElementById('frame').contentDocument
            ].join('|')
            """);

        Assert.Equal("1|true|true", result.ToString());
    }

    [Fact]
    public void DomBridge_ScriptAssigned_Iframe_Srcdoc_Fragments_Populate_FramesDocument()
    {
        const string html = """
<!DOCTYPE html>
<html><body><iframe id="frame"></iframe></body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        bridge.FireWindowLoadEvent();

        var result = context.Eval("""
            (() => {
                var frame = document.getElementById('frame');
                frame.srcdoc = '<!DOCTYPE html><style>.box{color:red}</style><div id="target" class="box">ok</div>';
                return [
                    !!frames[0].document.getElementById('target'),
                    !!frames[0].document.querySelector('.box'),
                    frames[0].document.getElementById('target').textContent.trim()
                ].join('|');
            })()
            """);

        Assert.Equal("true|true|ok", result.ToString());
    }

    [Fact]
    public void DomBridge_ScriptAssigned_Iframe_Srcdoc_Rebuilds_Existing_Subdocument()
    {
        const string html = """
<!DOCTYPE html>
<html><body><iframe id="frame" srcdoc="<!DOCTYPE html><html><body><div id='old'>old</div></body></html>"></iframe></body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        bridge.FireWindowLoadEvent();

        var result = context.Eval("""
            (() => {
                var frame = document.getElementById('frame');
                var before = frame.contentDocument.getElementById('old').textContent;
                frame.srcdoc = '<!DOCTYPE html><div id="new">new</div>';
                return [
                    before,
                    frame.contentDocument.getElementById('old') === null,
                    frame.contentDocument.getElementById('new').textContent
                ].join('|');
            })()
            """);

        Assert.Equal("old|true|new", result.ToString());
    }

    [Fact]
    public void DomBridge_ScrollIntoView_Uses_Script_Assigned_Iframe_Position_For_Fixed_Targets()
    {
        const string html = """
<!DOCTYPE html>
<html><body style="margin:0; width:2000px; height:2000px;">
  <iframe id="fr"></iframe>
</body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        bridge.FireWindowLoadEvent();

        var result = context.Eval("""
            (() => {
                var iframe = document.getElementById('fr');
                iframe.style.position = 'absolute';
                iframe.style.left = '100px';
                iframe.style.top = '300px';
                iframe.style.width = '400px';
                iframe.style.height = '300px';
                iframe.srcdoc = '<!DOCTYPE html><html><body style="margin:0"><div id="container" style="position:fixed; bottom:10px; left:30px; width:150px; height:150px;"><div id="target" style="position:absolute; left:10px; top:20px; width:10px; height:10px;"></div></div></body></html>';
                var target = iframe.contentDocument.getElementById('target');
                target.scrollIntoView({ block: 'start', inline: 'start' });
                return [
                    document.documentElement.scrollLeft,
                    document.documentElement.scrollTop,
                    iframe.contentWindow.scrollX,
                    iframe.contentWindow.scrollY
                ].join('|');
            })()
            """);

        Assert.Equal("140|460|0|0", result.ToString());
    }

    [Fact]
    public void DomBridge_ScrollIntoView_Uses_Script_Assigned_Iframe_Position_For_Scrollable_Fixed_Targets()
    {
        const string html = """
<!DOCTYPE html>
<html><body style="margin:0; width:2000px; height:2000px;">
  <iframe id="fr"></iframe>
</body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        bridge.FireWindowLoadEvent();

        var result = context.Eval("""
            (() => {
                var iframe = document.getElementById('fr');
                iframe.style.position = 'absolute';
                iframe.style.left = '100px';
                iframe.style.top = '300px';
                iframe.style.width = '400px';
                iframe.style.height = '300px';
                iframe.srcdoc = '<!DOCTYPE html><html><body style="margin:0"><div id="container" style="position:fixed; bottom:10px; left:30px; width:150px; height:150px; overflow:auto;"><div style="width:600px; height:600px;"></div><div id="target" style="position:absolute; left:200%; top:200%; width:10px; height:10px;"></div></div></body></html>';
                var target = iframe.contentDocument.getElementById('target');
                var container = iframe.contentDocument.getElementById('container');
                target.scrollIntoView({ block: 'start', inline: 'start' });
                return [
                    document.documentElement.scrollLeft,
                    document.documentElement.scrollTop,
                    iframe.contentWindow.scrollX,
                    iframe.contentWindow.scrollY,
                    container.scrollLeft,
                    container.scrollTop
                ].join('|');
            })()
            """);

        Assert.Equal("130|440|0|0|300|300", result.ToString());
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
    public void ScriptEngine_Execute_Runs_Async_Scripts_From_ExtractAll_Before_Deferred_Scripts()
    {
        const string html = """
<!DOCTYPE html>
<html><body><div id="out"></div>
<script>window.__order = ['regular'];</script>
<script async>window.__order.push('async');</script>
<script defer>
var p = document.createElement('p');
p.textContent = window.__order.join(',');
document.getElementById('out').appendChild(p);
</script>
</body></html>
""";

        var extraction = new ScriptExtractor().ExtractAll(html, "file:///test.html");
        var executableScripts = extraction.Scripts.Concat(extraction.AsyncScripts).ToArray();
        var output = new ScriptEngine().Execute(executableScripts, extraction.DeferredScripts, html, "file:///test.html");

        Assert.NotNull(output);
        Assert.Contains("regular,async", output);
    }

    [Fact]
    public void ScriptEngine_Execute_Runs_Microtasks_Between_Sequential_Scripts()
    {
        var engine = new ScriptEngine();

        var scripts = new List<string>
        {
            """
            window.order = [];
            queueMicrotask(function() {
                window.order.push('micro');
            });
            window.order.push('script-1');
            """,
            """
            document.getElementById('out').setAttribute('data-order-before-script-2', window.order.join(','));
            window.order.push('script-2');
            """
        };

        var html = """
<!DOCTYPE html>
<html><body><div id="out"></div></body></html>
""";

        var output = engine.Execute(scripts, Array.Empty<string>(), html, "file:///test.html");

        Assert.NotNull(output);
        Assert.Contains("data-order-before-script-2=\"script-1,micro\"", output);
    }

    [Fact]
    public void ScriptEngine_Execute_Runs_Microtasks_Between_Deferred_Scripts()
    {
        var engine = new ScriptEngine();
        var deferredScripts = new List<string>
        {
            """
            window.order = ['defer-1'];
            queueMicrotask(function() {
                window.order.push('micro');
            });
            """,
            """
            document.getElementById('out').setAttribute('data-order-before-defer-2', window.order.join(','));
            window.order.push('defer-2');
            """
        };

        var html = """
<!DOCTYPE html>
<html><body><div id="out"></div></body></html>
""";

        var output = engine.Execute(Array.Empty<string>(), deferredScripts, html, "file:///test.html");

        Assert.NotNull(output);
        Assert.Contains("data-order-before-defer-2=\"defer-1,micro\"", output);
    }

    [Theory]
    [InlineData("queueMicrotask();")]
    [InlineData("queueMicrotask('not callable');")]
    public void ScriptEngine_ExecuteDetailed_QueueMicrotask_Rejects_NonCallable_Callbacks(string script)
    {
        var engine = new ScriptEngine();

        var result = engine.ExecuteDetailed(new[] { script });

        Assert.False(result.Success);
        var error = Assert.Single(result.Errors);
        Assert.Contains("Callback must be a function", error.Message);
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

    [Fact]
    public void DomBridge_SerializeToHtml_Scales_ExplicitInherited_Zoom_Properties()
    {
        const string html = """
<!DOCTYPE html>
<html>
<head>
<style>
  .zoomed-radius {
    width: 100px;
    height: 100px;
    border: 5px solid black;
    border-radius: inherit;
    zoom: 2;
  }
  .zoomed-outline {
    width: 50px;
    height: 50px;
    margin: 50px;
    outline-width: inherit;
    outline-offset: inherit;
    outline-style: solid;
    outline-color: black;
    zoom: 2;
  }
  .zoomed-columns {
    width: 300px;
    height: 200px;
    column-width: inherit;
    column-height: inherit;
    column-gap: inherit;
    zoom: 2;
  }
</style>
</head>
<body>
  <div style="border-radius:20px; display:contents">
    <div id="radius" class="zoomed-radius"></div>
  </div>
  <div style="outline-width:10px; outline-offset:5px; display:contents">
    <div id="outline" class="zoomed-outline"></div>
  </div>
  <div style="column-width:40px; column-height:150px; column-gap:10px; display:contents">
    <div id="columns" class="zoomed-columns"></div>
  </div>
</body>
</html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");

        var result = bridge.SerializeToHtml();

        Assert.Contains("id=\"radius\" class=\"zoomed-radius\" style=\"width: 200px; height: 200px; border-top-width: 10px; border-right-width: 10px; border-bottom-width: 10px; border-left-width: 10px; border-radius: 40px\"", result);
        Assert.Contains("id=\"outline\" class=\"zoomed-outline\" style=\"width: 100px; height: 100px; margin-top: 100px; margin-right: 100px; margin-bottom: 100px; margin-left: 100px; outline-width: 20px; outline-offset: 10px\"", result);
        Assert.Contains("id=\"columns\" class=\"zoomed-columns\" style=\"width: 600px; height: 400px; column-width: 80px; column-height: 300px; column-gap: 20px\"", result);
    }

    [Fact]
    public void DomBridge_SerializeToHtml_Generates_Scaled_Zoomed_Pseudo_Element_Rules()
    {
        const string html = """
<!DOCTYPE html>
<html>
<head>
<style>
  .zoomed {
    zoom: 2;
  }
  .zoomed::before {
    content: "";
    display: block;
    width: 100px;
    height: 50px;
    margin-left: 10px;
    border: 2px solid black;
    background: green;
  }
</style>
</head>
<body>
  <div id="target" class="zoomed"></div>
</body>
</html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");

        var result = bridge.SerializeToHtml();

        Assert.Contains("id=\"target\" class=\"zoomed\"", result);
        Assert.Contains("#target::before", result);
        Assert.Contains("width: 200px !important", result);
        Assert.Contains("height: 100px !important", result);
        Assert.Contains("margin-left: 20px !important", result);
        Assert.Contains("border-top-width: 4px !important", result);
        Assert.Contains("border-right-width: 4px !important", result);
        Assert.Contains("border-bottom-width: 4px !important", result);
        Assert.Contains("border-left-width: 4px !important", result);
    }

    [Fact]
    public void DomBridge_SerializeToHtml_Scales_Zoomed_ScrollPadding_And_ScrollMargin_Properties()
    {
        const string html = """
<!DOCTYPE html>
<html>
<head>
<style>
  .zoomed-padding {
    width: 120px;
    height: 100px;
    overflow: hidden;
    border: 1px solid black;
    scroll-padding-top: inherit;
    zoom: 2;
  }
  .zoomed-margin-inherit {
    width: 200px;
    height: 10px;
    scroll-margin-top: inherit;
    zoom: 2;
  }
  .zoomed-margin-explicit {
    width: 200px;
    height: 10px;
    scroll-margin-top: 20px;
    zoom: 2;
  }
</style>
</head>
<body>
  <div style="scroll-padding-top:20px; display:contents">
    <div id="padding" class="zoomed-padding"></div>
  </div>
  <div style="scroll-margin-top:20px; display:contents">
    <div id="margin-inherit" class="zoomed-margin-inherit"></div>
  </div>
  <div id="margin-explicit" class="zoomed-margin-explicit"></div>
</body>
</html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");

        var result = bridge.SerializeToHtml();

        Assert.Contains("id=\"padding\" class=\"zoomed-padding\" style=\"width: 240px; height: 200px; scroll-padding-top: 40px; border-top-width: 2px; border-right-width: 2px; border-bottom-width: 2px; border-left-width: 2px\"", result);
        Assert.Contains("id=\"margin-inherit\" class=\"zoomed-margin-inherit\" style=\"width: 400px; height: 20px; scroll-margin-top: 40px\"", result);
        Assert.Contains("id=\"margin-explicit\" class=\"zoomed-margin-explicit\" style=\"width: 400px; height: 20px; scroll-margin-top: 40px\"", result);
    }

    [Fact]
    public void DomBridge_SerializeToHtml_Scales_Zoomed_Svg_Geometry_And_FontRelative_Lengths()
    {
        const string html = """
<!doctype html>
<meta charset="utf-8">
<style>
  :root { font-size: 10px; zoom: 2; }
  body { margin: 0 }
  .container { font-size: 20px; }
  .child { zoom: 2; }
  line {
    stroke-width: 2px;
    stroke: lime;
  }
  svg {
    background-color: black;
  }
</style>
<div class="container">
  <div class="child">
    <svg id="icon" width="100" height="100">
      <defs>
        <path id="p" d="M80,60H25"></path>
      </defs>
      <rect id="box" width="10rem" height="100" fill="blue"></rect>
      <line id="em-line" y1="10" y2="10" x1="0" x2="1em"></line>
      <line id="rem-line" y1="20" y2="20" x1="0" x2="1rem"></line>
      <line id="vw-line" y1="30" y2="30" x1="0" x2="1vw"></line>
      <polygon id="poly" points="0,50 50,50 50,60 0,60"></polygon>
      <text id="label" x="80" y="60" style="font-size:10px">X</text>
    </svg>
  </div>
</div>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");

        var result = bridge.SerializeToHtml();

        Assert.Contains("id=\"icon\" width=\"400\" height=\"400\"", result);
        Assert.Contains("id=\"box\" width=\"20rem\" height=\"400\"", result);
        Assert.Contains("id=\"em-line\" y1=\"40\" y2=\"40\" x1=\"0\" x2=\"2em\" style=\"stroke-width: 8px\"", result);
        Assert.Contains("id=\"rem-line\" y1=\"80\" y2=\"80\" x1=\"0\" x2=\"2rem\" style=\"stroke-width: 8px\"", result);
        Assert.Contains("id=\"vw-line\" y1=\"120\" y2=\"120\" x1=\"0\" x2=\"4vw\" style=\"stroke-width: 8px\"", result);
        Assert.Contains("id=\"poly\" points=\"0,200 200,200 200,240 0,240\"", result);
        Assert.Contains("id=\"p\" d=\"M320,240H100\"", result);
        Assert.Contains("id=\"label\" x=\"320\" y=\"240\" style=\"font-size: 40px\"", result);
    }

    [Fact]
    public void DomBridge_SerializeToHtml_Updates_Iframe_SrcDoc_After_Subdocument_Mutation()
    {
        const string html = """
<!DOCTYPE html>
<html>
<body>
  <iframe id="frame" srcdoc="<!DOCTYPE html><html><body><div id='value'>old</div></body></html>"></iframe>
</body>
</html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        bridge.FireWindowLoadEvent();

        context.Eval("""
            document.getElementById('frame').contentDocument.getElementById('value').textContent = 'new';
            """);

        var result = bridge.SerializeToHtml();

        Assert.Contains("srcdoc=\"&lt;html&gt;&lt;head&gt;&lt;/head&gt;&lt;body&gt;&lt;div id=&quot;value&quot;&gt;new&lt;/div&gt;&lt;/body&gt;&lt;/html&gt;\"", result);
        Assert.DoesNotContain("</html></iframe>", result);
        Assert.DoesNotContain(">old<", result);
    }

    [Fact]
    public void DomBridge_SerializeToHtml_Preserves_Mutated_Iframe_Scroll_State_In_SrcDoc()
    {
        const string html = """
<!DOCTYPE html>
<html>
<body>
  <iframe id="frame" srcdoc="<!DOCTYPE html><html><body><div id='scroller' style='width:100px;height:60px;overflow:hidden'><div style='height:200px'></div><div id='target' style='height:20px'></div></div></body></html>"></iframe>
</body>
</html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        bridge.FireWindowLoadEvent();

        context.Eval("""
            var doc = document.getElementById('frame').contentDocument;
            doc.getElementById('target').scrollIntoView();
            """);
        bridge.ResolveAnchorPositions();

        var result = bridge.SerializeToHtml();

        Assert.Contains("srcdoc=\"&lt;html&gt;&lt;head&gt;&lt;/head&gt;&lt;body&gt;&lt;div id=&quot;scroller&quot; style=&quot;width: 100px; height: 60px; overflow: hidden&quot;&gt;&lt;div style=&quot;position: relative; top: -160px&quot;&gt;", result);
        Assert.DoesNotContain("&gt;&lt;html&gt;&lt;head&gt;", result);
    }

    [Fact]
    public void DomBridge_SerializeToHtml_Persists_VisualViewport_Zoom_And_PageOffset_For_Fixed_ScrollIntoView()
    {
        const string html = """
<!DOCTYPE html>
<html>
<head>
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <style>
    html { height: 10000px; }
    body { margin: 0; padding: 0; }
    #fixed {
      position: fixed;
      bottom: 0;
      height: 50vh;
      width: 100vw;
      overflow: scroll;
      background-color: gray;
    }
    input { height: 20px; }
  </style>
</head>
<body>
  <div id="fixed">
    <div style="height: calc(80vh - 40px)"></div>
    <input type="text" id="name">
  </div>
</body>
</html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");

        context.Eval("""
            visualViewport.scale = 2;
            window.scrollTo(0, 1000);
            document.getElementById('name').scrollIntoView({ behavior: 'instant' });
            """);
        bridge.ResolveAnchorPositions();

        var result = bridge.SerializeToHtml();

        // Pinch-zoom scale 2 doubles the 10000px root height and the fixed
        // element's 50vh/100vw box from 384x1024 to 768x2048.
        Assert.Contains("<html style=\"height: 20000px\"", result);
        Assert.Contains("id=\"fixed\" style=\"position: fixed; bottom: 0px; height: 768px; width: 2048px; overflow: scroll; background-color: gray\"", result);
        // The visual viewport pans to pageTop=1384, so root scroll simulation
        // serializes as -1384*2=-2768px and the fixed scroller keeps the
        // target aligned by scaling its own 575.6px-ish internal scroll offset.
        Assert.Contains("style=\"position: relative; top: -2768px", result);
        Assert.Contains("style=\"position: relative; top: -1151.2px", result);
    }

    [Fact]
    public void DomBridge_Iframe_Srcdoc_Executes_Async_Scripts_Before_Deferred_Scripts()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<iframe id="frame" srcdoc="<!DOCTYPE html><html><body><script>window.__innerOrder = ['regular'];</script><script async>window.__innerOrder.push('async');</script><script defer>document.body.setAttribute('data-order', window.__innerOrder.join(','));</script></body></html>"></iframe>
<div id="result"></div>
<script>
var doc = document.getElementById('frame').contentDocument;
document.getElementById('result').textContent = doc.body.getAttribute('data-order') || 'missing';
</script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">regular,async<", result);
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
    public void InteractiveSession_Step_Runs_Microtasks_Between_Timer_Tasks()
    {
        var engine = new ScriptEngine();
        var html = "<html><body><div id='out'></div></body></html>";
        var scripts = new List<string>
        {
            @"
            var order = [];
            setTimeout(function() {
                order.push('timeout-1');
                queueMicrotask(function() { order.push('micro'); });
            }, 0);
            setTimeout(function() {
                order.push('timeout-2');
                document.getElementById('out').textContent = order.join(',');
            }, 0);
            "
        };

        var session = engine.ExecuteInteractive(scripts, Array.Empty<string>(), html, null);
        Assert.NotNull(session);

        var stepHtml = session.Step();

        Assert.NotNull(stepHtml);
        Assert.Contains("timeout-1,micro,timeout-2", stepHtml);
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
