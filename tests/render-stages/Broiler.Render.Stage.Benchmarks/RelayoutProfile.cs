using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Broiler.Dom;
using Broiler.Dom.Html;
using Broiler.Graphics;
using Broiler.HTML.Image;
using Broiler.Layout.Diagnostics;
using Broiler.Layout.Engine;
using BBitmap = Broiler.HTML.Image.BBitmap;

namespace Broiler.Render.Stage.Benchmarks;

/// <summary>
/// What a <em>second</em> layout costs after the document is mutated the way script mutates it.
/// The precondition multithreading roadmap §7 names for item #14, built before any dirty bit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why item #14 could not be started without this.</b> P0-a renders each corpus page once, from
/// a clean container, at a fixed viewport — so nothing it measures performs a second layout, and a
/// second layout is the entire thing dirty bits bound. The item's own estimate says as much
/// ("5–50× on interactive relayout — <em>unmeasured</em>"), and §7 recorded the harness as the
/// first thing Phase 3's remainder should build. Writing a dirty bit against an unmeasured stage
/// would be optimising a number nobody has seen.
/// </para>
/// <para>
/// <b>The mutation is applied to the DOM, because that is the seam a relayout actually arrives
/// through.</b> <c>HtmlContainerInt</c> holds a bound <see cref="DomDocument"/> and a copy of its
/// <see cref="DomDocument.Version"/>; <c>EnsureBoundDocumentCurrent</c> compares the two at the top
/// of every <c>PerformLayout</c> and, when they differ, calls <c>BuildBoundDocument</c> — which
/// disposes the render tree and regenerates it from scratch. So "relayout" in this engine today is
/// not a layout pass at all: it is a full box-tree rebuild and a full cascade, and then a
/// full-tree layout. Driving the mutation through <c>SetHtml</c> instead would have measured a
/// re-parse of a source string, which is a different thing that no script does.
/// </para>
/// <para>
/// <b>The split is the point, not the total.</b> The rebuild's internals are already instrumented
/// — <see cref="RenderStageTrace"/> wraps the HTML parse, the CSS parse, both cascade halves and
/// the box fixups inside <c>DomParser.GenerateCssTree</c>, which is exactly the call
/// <c>BuildBoundDocument</c> makes — so the traced total <em>is</em> the rebuild and the residual
/// is the layout pass proper. That is the number item #14 needs before it picks a target: if the
/// rebuild dominates, then dirty bits on the <em>layout</em> bound the smaller half, and the item
/// as written is aimed at the wrong stage.
/// </para>
/// <para>
/// <b>Against a <c>Broiler.HTML</c> without the sub-stage scopes the trace is silent and this
/// reports totals only.</b> The scopes live in that submodule (they arrived as <c>patches/0129</c>
/// and are upstream now); <see cref="RenderStageTrace"/> itself is in this repository, so the file
/// always compiles. A run whose sub-stage row is empty is a run against a submodule tree that
/// predates them, and it says so rather than reporting zeros as measurements.
/// </para>
/// <para>
/// <b>Four mutations, chosen for how much of the tree they can possibly affect.</b> A class toggle
/// on one deep element and an inline-style write on one deep element are the smallest edits a
/// script makes; an inserted subtree adds boxes; a text write changes one text node's measured
/// size. All four bump the document version identically, which is itself the finding this harness
/// exists to expose — the engine cannot tell them apart, so it does the same total work for all
/// four.
/// </para>
/// <para>
/// <b>Three more, added when item #14 was picked up.</b> The first publication of this harness
/// named two cases it deliberately did not cover, on the grounds that adding them then would have
/// been choosing the fixture that flatters the conclusion before the conclusion was being tested.
/// They are here now, with a third that the first two implied:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <c>detached build</c> — twenty-four nodes created and assembled <em>off</em> the document and
/// never inserted, which is what every <c>DocumentFragment</c> population and every build-then-
/// insert does on its way to the tree. Nothing the render tree shows can have changed, and it is
/// the honest form of the "changes nothing observable" case: a same-value attribute or text write
/// never reaches the version counter at all, because <c>Broiler.DOM</c> returns before publishing
/// when the value is unchanged. This is the row <see cref="RenderTreeInvalidation"/> elides.
/// </description></item>
/// <item><description>
/// <c>burst (20 writes)</c> — twenty connected writes before one layout, the coalescing case. Not a
/// saving: the version compare already collapses a burst into one rebuild. It was added on the
/// expectation that the layout share would <em>rise</em> as one rebuild was divided across twenty
/// edits, and it measured 2.4% against 2.5% on the rule-heavy page — flat, because the rebuild is a
/// whole-document re-cascade for one attribute write and there is nothing per-mutation in it to
/// amortise. The row earns its place by making that a null result instead of an expectation.
/// </description></item>
/// <item><description>
/// <c>unstyled attribute</c> — one <c>data-*</c> write on a connected element, which no selector in
/// any corpus page can reach. It still costs a full rebuild, and it is here to size what is left on
/// the table: eliding it needs the cascade to answer whether a rule's subject could match
/// differently, which is the rest of item #14 rather than a connectivity test.
/// </description></item>
/// </list>
/// <para>
/// <b>The rebuild column is now a fact rather than an inference, and it has three states rather
/// than two.</b> A row that skips the rebuild is otherwise indistinguishable from a row that
/// performed a fast one, so each row reports the decision <see cref="RenderTreeInvalidation"/>
/// recorded — the absence of work, stated. The third state exists because the first draft of that
/// column did not have it and was wrong on the run that mattered most: against a
/// <c>Broiler.HTML</c> without the ledger nothing consults it, both counters stay at zero, and a
/// two-valued column reports the baseline — where every row rebuilds — as entirely elided.
/// </para>
/// </remarks>
internal static class RelayoutProfile
{
    private const int Width = Corpus.ViewportWidth;
    private const int Height = Corpus.ViewportHeight;

    /// <summary>One mutation, named for what a script would have been doing.</summary>
    private sealed record Mutation(string Name, Action<DomDocument> Apply);

    /// <summary>One page's figures for one mutation.</summary>
    private sealed record Row(
        string Page,
        string Mutation,
        double FirstLayoutMs,
        double RelayoutMs,
        double RebuildMs,
        Verdict Rebuilt,
        IReadOnlyDictionary<string, double> SubStages);

    /// <summary>
    /// What the container decided about the rebuild — <em>including</em> the case where it decided
    /// nothing.
    /// </summary>
    /// <remarks>
    /// The third state is not padding. A submodule tree that predates the ledger consults it
    /// nowhere, so both decision counters stay at zero — and a two-valued column renders that as
    /// "elided", which is the exact opposite of what happened: every row rebuilt. The baseline run
    /// this harness is compared against is such a tree, so the column has to be able to say "there
    /// was no decision" or the comparison reads backwards.
    /// </remarks>
    private enum Verdict
    {
        /// <summary>No decision was recorded — a <c>Broiler.HTML</c> without the ledger wired in.</summary>
        NoLedger,

        /// <summary>The render tree was rebuilt.</summary>
        Rebuilt,

        /// <summary>The rebuild was skipped.</summary>
        Elided,
    }

    public static int Run(int iterations, int warmup)
    {
        var traceWasEnabled = RenderStageTrace.Enabled;
        RenderStageTrace.Enabled = true;
        try
        {
            var rows = new List<Row>();
            foreach (var page in Corpus.Pages)
            {
                foreach (var mutation in Mutations)
                {
                    var samples = new List<Row>(iterations);
                    for (var i = 0; i < warmup; i++)
                        MeasureOnce(page, mutation);
                    for (var i = 0; i < iterations; i++)
                        samples.Add(MeasureOnce(page, mutation));

                    rows.Add(Summarize(page.Name, mutation.Name, samples));
                }
            }

            Report(rows);
            return 0;
        }
        finally
        {
            RenderStageTrace.Enabled = traceWasEnabled;
        }
    }

    /// <summary>
    /// One first layout and one relayout, on a container that has never seen either before.
    /// </summary>
    /// <remarks>
    /// A fresh container per sample, deliberately: reusing one would let the second sample's
    /// "first layout" read a warmed font cache and a warmed style engine that the first sample's
    /// did not have, which is the comparison this harness is about.
    /// </remarks>
    private static Row MeasureOnce(Corpus.Page page, Mutation mutation)
    {
        var clip = new RectangleF(0, 0, Width, Height);
        var document = ParseDocument(page);

        using var bitmap = new BBitmap(Width, Height);
        using var container = new HtmlContainer();
        container.Location = new PointF(0, 0);
        container.MaxSize = new SizeF(Width, Height);
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;

        container.SetDocumentWithStyleSet(document, null, null);
        bitmap.Clear(BColor.White);

        var first = Stopwatch.StartNew();
        container.PerformLayout(bitmap, clip);
        first.Stop();

        mutation.Apply(document);

        // The trace is reset here rather than at the top: the first layout also rebuilds (the
        // document is bound but has never been built), and charging its sub-stages to the relayout
        // would double every figure below. The rebuild decision is zeroed with it, for the same
        // reason and at the same point.
        RenderStageTrace.Reset();
        RenderTreeInvalidation.Decisions.Reset();

        var second = Stopwatch.StartNew();
        container.PerformLayout(bitmap, clip);
        second.Stop();

        var subStages = RenderStageTrace.Totals();
        var rebuild = subStages.Values.Sum();

        return new Row(
            page.Name,
            mutation.Name,
            first.Elapsed.TotalMilliseconds,
            second.Elapsed.TotalMilliseconds,
            rebuild,
            RenderTreeInvalidation.Decisions.Required > 0 ? Verdict.Rebuilt
                : RenderTreeInvalidation.Decisions.Elided > 0 ? Verdict.Elided
                : Verdict.NoLedger,
            subStages);
    }

    private static DomDocument ParseDocument(Corpus.Page page)
    {
        var parser = new HtmlDocumentParser();
        return parser.ParseDocument(page.Html).Document;
    }

    /// <summary>
    /// The mutations, in ascending order of how much of the tree they could justify touching. The
    /// first four cost the same as each other, which was the harness's first result rather than an
    /// assumption; the last three were added when item #14 was picked up, and only
    /// <c>detached build</c> is answered without a rebuild.
    /// </summary>
    private static readonly Mutation[] Mutations =
    [
        new("class toggle", document =>
        {
            var target = DeepestElement(document);
            target?.SetAttribute("class", "relayout-toggled");
        }),
        new("inline style write", document =>
        {
            var target = DeepestElement(document);
            target?.SetAttribute("style", "color:#123456");
        }),
        new("text write", document =>
        {
            // The text node itself, not the element: writing an element's textContent replaces its
            // children, which is an insertion/removal as well as a text change and would measure
            // two mutations as one.
            var target = DeepestElement(document)?.ChildNodes.OfType<DomText>().FirstOrDefault()
                ?? FirstText(document);
            if (target != null)
                target.Data = "relayout";
        }),
        new("inserted subtree", document =>
        {
            var host = DeepestElement(document)?.ParentNode ?? document.DocumentElement;
            if (host == null)
                return;

            var inserted = document.CreateElement("div");
            inserted.SetAttribute("style", "width:120px;height:40px");
            inserted.AppendChild(document.CreateElement("span"));
            host.AppendChild(inserted);
        }),
        new("detached build", document =>
        {
            // Built and never inserted. Twenty-four version bumps, none of which any render tree
            // built from this document could show — the case item #14's first slice elides, and the
            // shape of every DocumentFragment population and every build-then-insert. The insert is
            // deliberately absent: it is a connected mutation and would rebuild, which is correct
            // and would hide the half being measured here.
            var detached = document.CreateElement("div");
            for (var i = 0; i < 8; i++)
            {
                var row = document.CreateElement("p");
                row.SetAttribute("class", "detached-row");
                row.AppendChild(document.CreateTextNode("row " + i.ToString(CultureInfo.InvariantCulture)));
                detached.AppendChild(row);
            }
        }),
        new("burst (20 writes)", document =>
        {
            // Twenty connected edits before one layout. Not a saving — the version compare already
            // collapses a burst into a single rebuild — but it is the case the first publication of
            // this harness said it did not cover, and what it shows is the layout share rising as
            // one rebuild is divided across twenty edits instead of one.
            var targets = ConnectedElements(document, 20);
            for (var i = 0; i < targets.Count; i++)
                targets[i].SetAttribute("class", "burst-" + i.ToString(CultureInfo.InvariantCulture));
        }),
        new("unstyled attribute", document =>
        {
            // A data-* write no corpus selector can reach. It still rebuilds, and that is the point
            // of the row: it sizes what a connectivity test cannot elide and an invalidation set
            // could.
            var target = DeepestElement(document);
            target?.SetAttribute("data-relayout-probe", "1");
        }),
    ];

    /// <summary>The first <paramref name="count"/> elements of the document, in document order.</summary>
    private static List<DomElement> ConnectedElements(DomDocument document, int count)
    {
        var found = new List<DomElement>(count);

        void Walk(DomNode node)
        {
            if (found.Count >= count)
                return;

            if (node is DomElement element)
                found.Add(element);

            foreach (var child in node.ChildNodes)
                Walk(child);
        }

        Walk(document);
        return found;
    }

    /// <summary>The document's first text node, for a page whose deepest element holds none.</summary>
    private static DomText? FirstText(DomNode node)
    {
        if (node is DomText text)
            return text;

        foreach (var child in node.ChildNodes)
        {
            var found = FirstText(child);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// The deepest element in the document, which is the least favourable target a dirty-bit scheme
    /// could be handed: everything above it is an ancestor whose own layout may have to be redone.
    /// </summary>
    private static DomElement? DeepestElement(DomDocument document)
    {
        DomElement? deepest = null;
        var bestDepth = -1;

        void Walk(DomNode node, int depth)
        {
            if (node is DomElement element && depth > bestDepth)
            {
                deepest = element;
                bestDepth = depth;
            }

            foreach (var child in node.ChildNodes)
                Walk(child, depth + 1);
        }

        Walk(document, 0);
        return deepest;
    }

    private static Row Summarize(string page, string mutation, List<Row> samples)
    {
        var subStages = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var name in samples.SelectMany(s => s.SubStages.Keys).Distinct())
            subStages[name] = Median(samples.Select(s => s.SubStages.TryGetValue(name, out var v) ? v : 0).ToArray());

        return new Row(
            page,
            mutation,
            Median(samples.Select(s => s.FirstLayoutMs).ToArray()),
            Median(samples.Select(s => s.RelayoutMs).ToArray()),
            Median(samples.Select(s => s.RebuildMs).ToArray()),
            // Any sample that rebuilt makes the row a rebuilding one. The decision is a function of
            // the mutation and not of timing, so the samples agree; taking "any" rather than
            // "majority" means a disagreement shows up as a rebuild rather than being averaged away.
            samples.Any(s => s.Rebuilt == Verdict.Rebuilt) ? Verdict.Rebuilt
                : samples.Any(s => s.Rebuilt == Verdict.Elided) ? Verdict.Elided
                : Verdict.NoLedger,
            subStages);
    }

    private static double Median(double[] values)
    {
        var copy = (double[])values.Clone();
        Array.Sort(copy);
        var mid = copy.Length / 2;
        return copy.Length % 2 == 1 ? copy[mid] : (copy[mid - 1] + copy[mid]) / 2;
    }

    private static void Report(List<Row> rows)
    {
        var traced = rows.Any(r => r.SubStages.Count > 0);

        Console.WriteLine();
        Console.WriteLine("Relayout profile — the second layout after a script-shaped mutation");
        Console.WriteLine($"Viewport {Width}x{Height}. Medians in ms.");
        if (!traced)
        {
            Console.WriteLine();
            Console.WriteLine("  NOTE: no sub-stage timings. The RenderStageTrace scopes live in");
            Console.WriteLine("  Broiler.HTML; this submodule tree predates them, so the rebuild/layout");
            Console.WriteLine("  split below is unavailable.");
        }

        Console.WriteLine();
        Console.WriteLine($"{"page",-8} {"mutation",-20} {"1st layout",12} {"relayout",10} {"rebuild",10} {"layout",10} {"rebuild %",10} {"rebuilt?",10}");
        Console.WriteLine(new string('-', 95));

        foreach (var row in rows)
        {
            var layout = Math.Max(0, row.RelayoutMs - row.RebuildMs);
            var share = row.RelayoutMs > 0 ? row.RebuildMs / row.RelayoutMs : 0;
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-8} {1,-20} {2,12:F2} {3,10:F2} {4,10:F2} {5,10:F2} {6,9:P1} {7,10}",
                row.Page,
                row.Mutation,
                row.FirstLayoutMs,
                row.RelayoutMs,
                row.RebuildMs,
                layout,
                share,
                row.Rebuilt switch
                {
                    Verdict.Rebuilt => "yes",
                    Verdict.Elided => "ELIDED",
                    _ => "n/a",
                }));
        }

        if (rows.All(r => r.Rebuilt == Verdict.NoLedger))
        {
            Console.WriteLine();
            Console.WriteLine("  NOTE: the rebuilt? column reads n/a on every row. The container never");
            Console.WriteLine("  consulted RenderTreeInvalidation, so this is a Broiler.HTML tree that");
            Console.WriteLine("  predates item #14's ledger (it arrives with patches/0131) — every row");
            Console.WriteLine("  above rebuilt, which is what this run is a baseline for.");
        }

        if (!traced)
            return;

        Console.WriteLine();
        Console.WriteLine("Rebuild sub-stages (medians, ms):");
        var names = rows.SelectMany(r => r.SubStages.Keys).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Console.WriteLine($"{"page",-8} {"mutation",-20} " + string.Join(" ", names.Select(n => n.PadLeft(20))));
        foreach (var row in rows)
        {
            Console.WriteLine(
                $"{row.Page,-8} {row.Mutation,-20} " +
                string.Join(" ", names.Select(n =>
                    (row.SubStages.TryGetValue(n, out var v) ? v : 0).ToString("F2", CultureInfo.InvariantCulture).PadLeft(20))));
        }
    }
}
