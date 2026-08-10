using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Broiler.Wpt;

/// <summary>
/// An opt-in, off-by-default breakdown of where a WPT test's wall time goes, phase by phase.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> <c>wpt-sequential-wins.md</c> measured a reftest at ~2.2–2.5 CPU-s and,
/// having ruled out I/O and process startup, attributed the remainder to per-render fixed cost
/// "inside the engine". <c>render-fixed-cost.md</c> then measured the engine directly through the
/// same entry the runner calls (<c>HtmlRender.RenderToImageWithStyleSet</c> at 1024x768) and found
/// an empty document costs <b>3.5 ms</b> and a WPT-median one 15–19 ms — two orders of magnitude
/// short of what the runner spends. So the time is real, CPU-bound, and <em>not</em> in the render.
/// The only honest way to find it is to time the phases the runner actually performs, which is what
/// this does.
/// </para>
/// <para>
/// <b>Phases are named for what they are, not for where they are called.</b> A reftest renders twice
/// — the test and its reference — through the same helper, so both land in the same buckets and the
/// totals below are per <em>run</em>, not per render. That is deliberate: the question is what a
/// *test* costs, and splitting test-render from reference-render would answer a question nobody
/// asked while hiding that the two are the same code.
/// </para>
/// <para>
/// <b>Cost when off, which is always in a normal run.</b> <see cref="Measure"/> reads one static
/// <see langword="bool"/> and returns a struct whose <see cref="Scope.Dispose"/> is a null check —
/// the same shape as <c>Broiler.Layout.Diagnostics.RenderStageTrace</c>, for the same reason.
/// </para>
/// <para>
/// <b>The accumulator is shared and locked, unlike <c>RenderStageTrace</c>'s, and the difference is
/// the scope size.</b> That type keeps a <c>[ThreadStatic]</c> map because it times four sub-stages
/// per render and a lock would have been a measurable share of what it measures. The phases here are
/// whole renders, script passes and pixel comparisons — milliseconds to seconds — so a lock per scope
/// is far below the noise, and being shared is what makes the totals *readable*: even in-process the
/// runner drains its queue on pool threads, so a per-thread accumulator reports nothing to the thread
/// that prints. (Measured the hard way: the first version was <c>[ThreadStatic]</c> and reported
/// "enabled but recorded nothing".)
/// </para>
/// <para>
/// It still needs <c>--no-worker-isolation</c>, because a worker <em>process</em>'s totals die with
/// it and are never sent back over the protocol.
/// </para>
/// </remarks>
internal static class WptPhaseTrace
{
    /// <summary>Phase names. These strings are the vocabulary the published breakdown uses.</summary>
    internal static class Phases
    {
        /// <summary>Reading the test (and reference) file off disk.</summary>
        public const string FileRead = "file read";

        /// <summary>Registering the WPT font faces. Cached after the first call; measured to prove it.</summary>
        public const string Fonts = "font registration";

        /// <summary>
        /// <c>ExecuteScriptsWithDom</c> — building a DOM, running the document's classic scripts
        /// through the JS engine, and serializing the mutated tree back to markup.
        /// </summary>
        public const string Scripts = "scripts + DOM bridge";

        /// <summary>`HtmlPostProcessor.Process` on the serialized markup.</summary>
        public const string PostProcess = "post-process";

        /// <summary>The render proper — `HtmlRender.RenderToImageWithStyleSet`.</summary>
        public const string Render = "render";

        /// <summary>`PixelDiffRunner.Compare` between the two bitmaps.</summary>
        public const string Compare = "pixel compare";

        /// <summary>Mismatch classification, band analysis and failure-image writing — failures only.</summary>
        public const string Diagnose = "failure diagnostics";

        // --- Sub-phases of Scripts. These nest inside it, so they are reported separately and
        // must not be added to the top-level total; Report() knows the difference.

        /// <summary>Inlining linked stylesheets and regex-scanning the document for scripts.</summary>
        public const string ScriptScan = "  ├ script scan + sheet inlining";

        /// <summary>Constructing the <c>JSContext</c> — the JS engine's per-document world.</summary>
        public const string JsContext = "  ├ JSContext construction";

        /// <summary>`DomBridge.Attach` — parse, DOM build, and the bridge's per-document session.</summary>
        public const string BridgeAttach = "  ├ DomBridge.Attach";

        /// <summary>Evaluating the document's scripts, including the always-injected stubs.</summary>
        public const string ScriptEval = "  ├ script eval + drains";

        /// <summary>Window load, animation snapshots, anchor positions, check-layout assertions.</summary>
        public const string PostScript = "  ├ load event + snapshots + anchors";

        /// <summary>`DomBridge.SerializeToHtml` — the mutated DOM back to markup.</summary>
        public const string Serialize = "  └ SerializeToHtml";
    }

    private static readonly Dictionary<string, (double Ms, int Count)> _accumulator = new(StringComparer.Ordinal);
    private static readonly object _sync = new();

    /// <summary>Whether the phase timers run. Off by default.</summary>
    public static bool Enabled { get; set; }

    /// <summary>Discards the accumulated totals.</summary>
    public static void Reset()
    {
        lock (_sync)
            _accumulator.Clear();
    }

    /// <summary>The accumulated phase totals since the last <see cref="Reset"/>. A copy.</summary>
    public static IReadOnlyDictionary<string, (double Ms, int Count)> Totals()
    {
        lock (_sync)
            return new Dictionary<string, (double, int)>(_accumulator);
    }

    /// <summary>Times the <see langword="using"/> block into <paramref name="phase"/> when enabled.</summary>
    public static Scope Measure(string phase) => Enabled ? new Scope(phase) : default;

    private static void Add(string phase, long startTimestamp)
    {
        var ms = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
        lock (_sync)
        {
            _accumulator.TryGetValue(phase, out var previous);
            _accumulator[phase] = (previous.Ms + ms, previous.Count + 1);
        }
    }

    /// <summary>One timed phase. <see langword="default"/> is the disabled, do-nothing form.</summary>
    public readonly struct Scope : IDisposable
    {
        private readonly string? _phase;
        private readonly long _start;

        internal Scope(string phase)
        {
            _phase = phase;
            _start = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            if (_phase is not null)
                Add(_phase, _start);
        }
    }

    /// <summary>
    /// Writes the breakdown as markdown. <paramref name="wallMs"/> is the run's own wall clock, so
    /// the residual — everything the phases do not name — is reported rather than left implied.
    /// </summary>
    public static void Report(System.IO.TextWriter output, double wallMs, int testCount)
    {
        // Merged, not printed separately: the bridge's own scopes nest inside DomBridge.Attach,
        // which nests inside Scripts, and a reader comparing three tables would have to do the
        // nesting arithmetic by hand. Report() already knows which rows are nested.
        var totals = new Dictionary<string, (double Ms, int Count)>(Totals(), StringComparer.Ordinal);
        foreach (var (phase, entry) in Broiler.HtmlBridge.Core.Diagnostics.BridgePhaseTrace.Totals())
            totals[phase] = entry;
        if (totals.Count == 0)
        {
            output.WriteLine("Phase trace was enabled but recorded nothing.");
            return;
        }

        var order = new[]
        {
            Phases.FileRead, Phases.Fonts, Phases.Scripts,
            Phases.ScriptScan, Phases.JsContext, Phases.BridgeAttach,
            Phases.ScriptEval, Phases.PostScript, Phases.Serialize,
            Phases.PostProcess, Phases.Render, Phases.Compare, Phases.Diagnose,
        };

        // DomBridge.Attach's own split, spliced in under it.
        order =
        [
            .. order[..order.ToList().IndexOf(Phases.ScriptEval)],
            Broiler.HtmlBridge.Core.Diagnostics.BridgePhaseTrace.Phases.ParseHtml,
            Broiler.HtmlBridge.Core.Diagnostics.BridgePhaseTrace.Phases.RegisterDocument,
            .. order[order.ToList().IndexOf(Phases.ScriptEval)..],
        ];

        // The sub-phases sit inside Scripts, so counting them into the attributed total would
        // double-count that whole phase and make the residual negative.
        var nested = new HashSet<string>(new[]
        {
            Phases.ScriptScan, Phases.JsContext, Phases.BridgeAttach,
            Phases.ScriptEval, Phases.PostScript, Phases.Serialize,
            Broiler.HtmlBridge.Core.Diagnostics.BridgePhaseTrace.Phases.ParseHtml,
            Broiler.HtmlBridge.Core.Diagnostics.BridgePhaseTrace.Phases.RegisterDocument,
        }, StringComparer.Ordinal);

        output.WriteLine();
        output.WriteLine("=== Phase breakdown ===");
        output.WriteLine($"{testCount} test(s), {wallMs / 1000.0:F1} s wall");
        output.WriteLine();
        output.WriteLine("| phase | total s | share of wall | calls | ms/call | ms/test |");
        output.WriteLine("|---|---:|---:|---:|---:|---:|");

        double named = 0;
        foreach (var phase in order)
        {
            if (!totals.TryGetValue(phase, out var entry))
                continue;
            if (!nested.Contains(phase))
                named += entry.Ms;
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"| {phase} | {entry.Ms / 1000.0:F2} | {entry.Ms / wallMs * 100:F1}% | {entry.Count} | {entry.Ms / Math.Max(1, entry.Count):F2} | {entry.Ms / Math.Max(1, testCount):F1} |"));
        }

        var residual = wallMs - named;
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"| **(unattributed)** | {residual / 1000.0:F2} | {residual / wallMs * 100:F1}% | | | {residual / Math.Max(1, testCount):F1} |"));
        output.WriteLine();
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Attributed to named phases: {named / wallMs * 100:F1}%"));
    }
}
