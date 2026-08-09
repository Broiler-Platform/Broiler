using System.Diagnostics;
using System.Text;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;

namespace Broiler.Cli.Tests;

/// <summary>
/// The measurement behind multithreading roadmap item #16: what compiling a document's scripts on a
/// thread budget, ahead of the loop that evaluates them, is worth.
/// </summary>
/// <remarks>
/// <para>
/// <b>The structural claim is asserted; the wall clock is reported.</b> This is the split item #17's
/// overlap tests established, and it applies here for the same reason: a stopwatch comparison
/// measures the host's scheduler as much as the change and fails on a loaded CI box for reasons
/// that have nothing to do with the code. What can be checked exactly is that the eval loop finds
/// every source already compiled — see
/// <c>ScriptCompileAheadTests.A_Compiled_Ahead_Source_Is_A_Cache_Hit_At_The_Consume_Site</c>, which
/// asserts it on the cache's own hit and compilation counters — and that no source was left for it
/// to compile inline, which is asserted below on the compile-ahead's counters.
/// </para>
/// <para>
/// <b>The fixture is compile-heavy and execution-light on purpose.</b> Item #16 moves compilation
/// and nothing else: evaluation stays on the calling thread in document order, because item #15
/// made the event loop single-threaded deliberately. A fixture whose scripts <em>ran</em> for a
/// long time would measure the part that cannot move and report a smaller number for a reason that
/// has nothing to do with this item's ceiling. So each script here is a few hundred function
/// declarations — real work for the front end and the emitter, almost none for the interpreter —
/// and the figure it produces is the compile stage's scaling, not a whole page's.
/// </para>
/// <para>
/// <b>What a real page looks like is the other half, and it is why the end-to-end column is
/// printed too.</b> On a page whose scripts are small the compile is a small share of the capture,
/// Amdahl bounds this hard, and that is the honest number to publish beside the stage one.
/// </para>
/// </remarks>
[Collection("ScriptCompileAhead")]
public sealed class ScriptCompileAheadOverlapTests : IDisposable
{
    private const int Scripts = 8;
    private const int FunctionsPerScript = 400;

    private readonly int _budget = ScriptCompileAhead.MaxDegreeOfParallelism;
    private readonly bool _diagnostics = ScriptCompileAhead.CollectDiagnostics;

    public void Dispose()
    {
        ScriptCompileAhead.MaxDegreeOfParallelism = _budget;
        ScriptCompileAhead.CollectDiagnostics = _diagnostics;
    }

    /// <summary>
    /// The claim, in the form that does not depend on how fast the machine is: with the budget on,
    /// every one of the document's sources was compiled by a worker, so the ordered eval loop
    /// compiled none of them. With it off, no worker exists and the loop compiles all of them
    /// exactly where it used to.
    /// </summary>
    [Fact]
    public void Every_Source_Is_Compiled_By_A_Worker_When_The_Budget_Is_On()
    {
        var html = CompileHeavyDocument();

        ScriptCompileAhead.ResetDiagnostics();
        ScriptCompileAhead.CollectDiagnostics = true;
        try
        {
            var overlapped = RunAt(4, html);

            var diagnostics = ScriptCompileAhead.Diagnostics;
            Assert.Equal(Scripts, diagnostics.ScriptsQueued);
            Assert.Equal(Scripts, diagnostics.ScriptsCompiled);
            Assert.Equal(0, diagnostics.ScriptsFailed);

            ScriptCompileAhead.ResetDiagnostics();
            var serial = RunAt(1, html);

            Assert.Equal(0, ScriptCompileAhead.Diagnostics.ScriptsQueued);

            // And the exit gate, on the very document the figures below are measured on.
            Assert.Equal(serial, overlapped);
        }
        finally
        {
            ScriptCompileAhead.CollectDiagnostics = false;
        }
    }

    /// <summary>
    /// The compile stage on its own, at every budget the exit gate covers: the same sources
    /// compiled into a fresh cache by an ordered inline loop, and by the compile-ahead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The stage is measured separately from the capture because it is the only thing that
    /// moved.</b> A whole-capture figure charges this item for the parse, the DOM build, the style
    /// pass, the layout and the serialization, none of which it touches, and on this host the
    /// capture's own run-to-run spread is wider than the difference being measured — five identical
    /// serial captures of the heavy fixture landed between 7.3 s and 9.3 s. That spread is not
    /// noise to be averaged away in the end-to-end number; it is a reason not to derive the
    /// stage's scaling from it. Both figures are printed, and
    /// <see cref="Serial_And_Overlapped_Wall_Clock_Are_Reported"/> is the one bounded by what a
    /// document actually contains.
    /// </para>
    /// <para>
    /// <b>The serial baseline is the real inline loop, not the budget-1 branch of this one.</b> It
    /// calls <c>CoreScript.Compile</c> per source in document order with the same location, cache
    /// and options the consuming <c>Eval</c> passes — which is what <c>Eval</c> does, minus the
    /// evaluation.
    /// </para>
    /// </remarks>
    [Fact]
    public void Compile_Stage_Scaling_Is_Reported()
    {
        const int pairs = 7;
        var sources = CompileHeavySources(Scripts, FunctionsPerScript);

        // One untimed pass each way: the first run through this path JITs the front end and the
        // emitter, and timing that measures the runtime warming up.
        MeasureSerialCompile(sources);
        MeasureAheadCompile(sources, 4);

        foreach (var budget in new[] { 2, 3, 4, 8 })
        {
            var ratios = new List<double>();
            var serials = new List<double>();
            var aheads = new List<double>();

            for (var i = 0; i < pairs; i++)
            {
                var serial = MeasureSerialCompile(sources).TotalMilliseconds;
                var ahead = MeasureAheadCompile(sources, budget).TotalMilliseconds;

                serials.Add(serial);
                aheads.Add(ahead);
                ratios.Add(serial / ahead);
            }

            Console.WriteLine(
                $"compile stage, {Scripts} sources x {FunctionsPerScript} functions, " +
                $"{pairs} interleaved pairs, budget {budget}: " +
                $"serial median {Median(serials):F1} ms, ahead median {Median(aheads):F1} ms, " +
                $"median paired speedup {Median(ratios):F2}x " +
                $"(serial {Min(serials):F1}-{Max(serials):F1}, ahead {Min(aheads):F1}-{Max(aheads):F1})");
        }

        Assert.True(true);
    }

    /// <summary>
    /// The whole capture, budget 1 against 4, on the compile-heavy fixture. This is the figure the
    /// stage measurement above is bounded by, and it is printed with its spread because on this
    /// host the spread is the point.
    /// </summary>
    [Fact]
    public void Serial_And_Overlapped_Wall_Clock_Are_Reported()
    {
        const int pairs = 7;
        var html = CompileHeavyDocument();

        // One untimed pass each way: the first run through this path JITs the bridge, the engine
        // and the compiler, and timing that measures the runtime warming up.
        RunAt(1, html);
        RunAt(4, html);

        var ratios = new List<double>();
        var serials = new List<double>();
        var overlaps = new List<double>();

        for (var i = 0; i < pairs; i++)
        {
            var serial = Measure(() => RunAt(1, html)).TotalMilliseconds;
            var overlapped = Measure(() => RunAt(4, html)).TotalMilliseconds;

            serials.Add(serial);
            overlaps.Add(overlapped);
            ratios.Add(serial / overlapped);
        }

        Console.WriteLine(
            $"capture end to end, {Scripts} scripts x {FunctionsPerScript} functions, " +
            $"{pairs} interleaved pairs, budget 4: " +
            $"serial median {Median(serials):F1} ms, overlapped median {Median(overlaps):F1} ms, " +
            $"median paired speedup {Median(ratios):F2}x " +
            $"(serial {Min(serials):F1}-{Max(serials):F1}, overlapped {Min(overlaps):F1}-{Max(overlaps):F1})");

        // Deliberately not an assertion on the ratio. The claim this file makes is the structural
        // one above; the time it saves is arithmetic, and asserting on it would make a scheduling
        // hiccup look like a regression.
        Assert.NotEmpty(ratios);
    }

    /// <summary>
    /// The ordered inline loop the eval loop performs today: one <c>CoreScript.Compile</c> per
    /// source, in document order, on this thread, into a cache nothing else has touched.
    /// </summary>
    private static TimeSpan MeasureSerialCompile(IReadOnlyList<string> sources)
    {
        using var context = new JSContext();
        var cache = context.CodeCache;
        var options = context.CompilationOptions;

        var stopwatch = Stopwatch.StartNew();
        foreach (var source in sources)
            CoreScript.Compile(source, null, args: null, codeCache: cache, compilationOptions: options);
        return stopwatch.Elapsed;
    }

    private static TimeSpan MeasureAheadCompile(IReadOnlyList<string> sources, int budget)
    {
        using var context = new JSContext();
        ScriptCompileAhead.MaxDegreeOfParallelism = budget;

        var stopwatch = Stopwatch.StartNew();
        using (var ahead = ScriptCompileAhead.Start(context, sources))
            ahead?.Join();
        return stopwatch.Elapsed;
    }

    private static double Min(List<double> values) => values.Min();

    private static double Max(List<double> values) => values.Max();

    /// <summary>
    /// The same measurement on a page whose scripts are the size a real page's are, so the
    /// published figure is bounded by what a document actually contains rather than by the
    /// fixture's ceiling.
    /// </summary>
    [Fact]
    public void Wall_Clock_On_A_Modestly_Scripted_Page_Is_Reported()
    {
        const int pairs = 5;
        var html = CompileHeavyDocument(scripts: 6, functionsPerScript: 30);

        RunAt(1, html);
        RunAt(4, html);

        var ratios = new List<double>();
        var serials = new List<double>();
        var overlaps = new List<double>();

        for (var i = 0; i < pairs; i++)
        {
            var serial = Measure(() => RunAt(1, html));
            var overlapped = Measure(() => RunAt(4, html));

            serials.Add(serial.TotalMilliseconds);
            overlaps.Add(overlapped.TotalMilliseconds);
            ratios.Add(serial.TotalMilliseconds / overlapped.TotalMilliseconds);
        }

        Console.WriteLine(
            "script compile-ahead, 6 scripts x 30 functions (a modestly scripted page), " +
            $"{pairs} interleaved pairs, budget 4: " +
            $"serial median {Median(serials):F1} ms, overlapped median {Median(overlaps):F1} ms, " +
            $"median paired speedup {Median(ratios):F2}x");

        Assert.NotEmpty(ratios);
    }

    private static string RunAt(int budget, string html)
    {
        ScriptCompileAhead.MaxDegreeOfParallelism = budget;
        return CaptureService.ExecuteScriptsWithDom(html, "file:///compile-ahead-bench.html");
    }

    private static double Median(List<double> values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length % 2 == 1
            ? sorted[sorted.Length / 2]
            : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }

    private static TimeSpan Measure(Action body)
    {
        var stopwatch = Stopwatch.StartNew();
        body();
        return stopwatch.Elapsed;
    }

    /// <summary>
    /// <paramref name="scripts"/> script sources, each declaring <paramref name="functionsPerScript"/>
    /// distinct functions and calling one of them. Distinct per script and per index, so no two
    /// sources collapse onto one cache entry — a fixture whose scripts were identical would measure
    /// the cache's deduplication rather than the compiles.
    /// </summary>
    private static List<string> CompileHeavySources(int scripts, int functionsPerScript)
    {
        var sources = new List<string>(scripts);

        for (var s = 0; s < scripts; s++)
        {
            var source = new StringBuilder();
            for (var f = 0; f < functionsPerScript; f++)
            {
                source.Append($$"""
                    function s{{s}}f{{f}}(a, b) {
                      var acc = a + {{f}};
                      for (var i = 0; i < b; i++) { acc = acc * 2 + i - {{s}}; if (acc > 1e9) acc = acc % 7919; }
                      var o = { k{{f}}: acc, tag: 's{{s}}f{{f}}', list: [acc, acc + 1, acc + 2] };
                      return o.list.map(function (v) { return v + o.k{{f}}; }).reduce(function (x, y) { return x + y; }, 0);
                    }
                    """);
            }

            source.Append($"window.r{s} = s{s}f0(1, 3);");
            if (s == scripts - 1)
                source.Append("document.getElementById('out').textContent = 'done';");
            sources.Add(source.ToString());
        }

        return sources;
    }

    /// <summary>The same sources, wrapped one per inline <c>&lt;script&gt;</c>.</summary>
    private static string CompileHeavyDocument(int scripts = Scripts, int functionsPerScript = FunctionsPerScript)
    {
        var html = new StringBuilder("<!DOCTYPE html><html><body><div id=\"out\"></div>");
        foreach (var source in CompileHeavySources(scripts, functionsPerScript))
            html.Append("<script>").Append(source).Append("</script>");
        return html.Append("</body></html>").ToString();
    }
}
