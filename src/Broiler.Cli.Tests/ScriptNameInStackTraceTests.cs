using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Logging;
using Broiler.HtmlBridge.Scripting;

namespace Broiler.Cli.Tests;

/// <summary>
/// A script names itself in the stack traces of the errors it raises.
/// <para>
/// <c>JSContext.Eval</c> takes the location it reports in stack frames, and every host passed none,
/// so every frame of every script read <c>vm.js</c>. A page's scripts were then indistinguishable
/// from one another: <c>Cannot set property eqid of undefined … at inline in vm.js:line 3</c> could
/// be any of a dozen scripts on a real page, one of which is a megabyte of minified vendor code, and
/// nothing in the trace narrowed it down. The labels the hosts already used for their profiling
/// entries and error messages are now the evaluation's location too, so all three agree.
/// </para>
/// </summary>
public sealed class ScriptNameInStackTraceTests
{
    private const string Html = "<!doctype html><html><body></body></html>";

    /// <summary>
    /// The failing script names itself in the trace, and it is the right one of several — the third
    /// script, not the two before it that ran cleanly. The exception the host logs carries the
    /// trace, so this asserts what a report pasted out of a debugger or a log would actually show.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AThrowingScript_NamesItselfInTheTrace()
    {
        var logged = new List<Exception>();
        void Handler(RenderLogEntry entry)
        {
            if (entry.Exception is { } ex)
                lock (logged) logged.Add(ex);
        }

        RenderLogger.EntryLogged += Handler;
        try
        {
            var engine = new ScriptEngine();
            using var session = engine.ExecuteInteractive(
                // The third script fails the way the google.com report did: a property set on a
                // base that exists as an expression but evaluates to undefined.
                ["var ok = 1;", "var alsoOk = 2;", "globalThis.missingNamespace.eqid = 3;"],
                [],
                Html,
                "https://example.com/");
        }
        finally
        {
            RenderLogger.EntryLogged -= Handler;
        }

        List<Exception> captured;
        lock (logged) captured = [.. logged];

        var trace = Assert.Single(captured, e => e.Message.Contains("eqid", StringComparison.Ordinal)).ToString();
        Assert.Contains(ScriptLabel.Inline(2), trace, StringComparison.Ordinal);
        Assert.DoesNotContain("vm.js", trace, StringComparison.Ordinal);
    }

    /// <summary>
    /// The label the trace carries is the one the profiler and the error log already used, so a
    /// report that quotes any of the three can be matched against the other two.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheTraceLabel_IsTheProfilingLabel()
    {
        var engine = new ScriptEngine { Profiler = new ScriptProfilingHook() };
        using var session = engine.ExecuteInteractive(
            ["var a = 1;", "var b = 2;"],
            ["var c = 3;"],
            Html,
            "https://example.com/");

        Assert.Equal(
            [ScriptLabel.Inline(0), ScriptLabel.Inline(1), ScriptLabel.Deferred(0)],
            engine.Profiler!.Entries.Select(e => e.Label).ToArray());
    }

    [Theory]
    [InlineData(0, "inline-0")]
    [InlineData(7, "inline-7")]
    public void InlineLabels_AreDocumentOrderWithinTheirBucket(int index, string expected) =>
        Assert.Equal(expected, ScriptLabel.Inline(index));

    [Theory]
    [InlineData(0, "deferred-0")]
    [InlineData(3, "deferred-3")]
    public void DeferredLabels_AreDocumentOrderWithinTheirBucket(int index, string expected) =>
        Assert.Equal(expected, ScriptLabel.Deferred(index));

    [Fact(Timeout = 600000)]
    public void ModuleLabels_AreKeyedByModuleId() =>
        Assert.Equal("module-https://example.com/m.js", ScriptLabel.Module("https://example.com/m.js"));
}
