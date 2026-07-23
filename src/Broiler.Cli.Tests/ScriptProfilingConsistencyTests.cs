using System.Collections.Generic;
using System.Linq;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Scripting;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 8 item 4: profiling is applied consistently. The <see cref="ScriptProfilingHook"/> attached to
/// <see cref="ScriptEngine.Profiler"/> previously saw only the inline scripts — the render loop timed
/// <c>inline-{i}</c> evaluations but ran deferred scripts and engine-driven module roots straight through
/// <c>context.Eval</c>/<c>RunScriptAsync</c> with no measurement, so a consumer got a misleading partial
/// timeline. Every script the engine runs now funnels through the same <c>RunMeasured</c> seam
/// (inline / deferred / module), and these tests — the hook's first real consumer — pin that.
/// </summary>
public class ScriptProfilingConsistencyTests
{
    private const string Url = "file:///profile.html";

    [Fact]
    public void Profiler_Records_Inline_And_Deferred_Scripts()
    {
        var engine = new ScriptEngine { Profiler = new ScriptProfilingHook() };
        var scripts = new List<string> { "var a = 1;", "var b = 2;" };
        var deferred = new List<string> { "var c = 3;" };
        const string html = "<!DOCTYPE html><html><head></head><body></body></html>";

        var output = engine.Execute(scripts, deferred, html, Url);

        Assert.NotNull(output);
        var labels = engine.Profiler!.Entries.Select(e => e.Label).ToList();
        // Both the regular scripts and the deferred script are timed — not just the inline ones.
        Assert.Equal(new[] { "inline-0", "inline-1", "deferred-0" }, labels);
        Assert.All(engine.Profiler!.Entries, e => Assert.True(e.Succeeded));
    }

    [Fact]
    public void Profiler_Records_Entries_On_The_DomLess_Execute_Path()
    {
        var engine = new ScriptEngine { Profiler = new ScriptProfilingHook() };
        var scripts = new List<string> { "var x = 1;", "var y = 2;" };

        var ok = engine.Execute(scripts);

        Assert.True(ok);
        Assert.Equal(new[] { "inline-0", "inline-1" }, engine.Profiler!.Entries.Select(e => e.Label));
    }

    [Fact]
    public void Profiler_Records_A_Throwing_Script_As_Failed()
    {
        var engine = new ScriptEngine { Profiler = new ScriptProfilingHook() };
        // A syntax/runtime error is caught by the engine and logged; the timing entry must still be
        // recorded, flagged Succeeded=false, so the profiler timeline stays complete.
        var scripts = new List<string> { "throw new Error('boom');" };

        engine.Execute(scripts);

        var entry = Assert.Single(engine.Profiler!.Entries);
        Assert.Equal("inline-0", entry.Label);
        Assert.False(entry.Succeeded);
    }

    [Fact]
    public void No_Profiler_Attached_Runs_Clean()
    {
        // The common case: no hook set. Scripts still run; nothing is measured, nothing throws.
        var engine = new ScriptEngine();
        var scripts = new List<string> { "var a = 1;" };
        var deferred = new List<string> { "var b = 2;" };
        const string html = "<!DOCTYPE html><html><head></head><body></body></html>";

        var output = engine.Execute(scripts, deferred, html, Url);

        Assert.NotNull(output);
        Assert.Null(engine.Profiler);
    }
}
