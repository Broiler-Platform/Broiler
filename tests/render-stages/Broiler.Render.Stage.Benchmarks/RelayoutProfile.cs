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
/// <b>Without <c>patches/0129</c> the trace is silent and this reports totals only.</b> The scopes
/// live in <c>Broiler.HTML</c>; <see cref="RenderStageTrace"/> itself is in this repository, so
/// the file always compiles. A run whose sub-stage row is empty is a run against a submodule tree
/// that does not carry the patch, and it says so rather than reporting zeros as measurements.
/// </para>
/// <para>
/// <b>Four mutations, chosen for how much of the tree they can possibly affect.</b> A class toggle
/// on one deep element and an inline-style write on one deep element are the smallest edits a
/// script makes; an inserted subtree adds boxes; a text write changes one text node's measured
/// size. All four bump the document version identically, which is itself the finding this harness
/// exists to expose — the engine cannot tell them apart, so it does the same total work for all
/// four.
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
        IReadOnlyDictionary<string, double> SubStages);

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
        // would double every figure below.
        RenderStageTrace.Reset();

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
            subStages);
    }

    private static DomDocument ParseDocument(Corpus.Page page)
    {
        var parser = new HtmlDocumentParser();
        return parser.ParseDocument(page.Html).Document;
    }

    /// <summary>
    /// The mutations, in ascending order of how much of the tree they could justify touching. All
    /// four cost the same today, which is the harness's first result rather than an assumption.
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
    ];

    /// <summary>
    /// The deepest element in the document, which is the least favourable target a dirty-bit scheme
    /// could be handed: everything above it is an ancestor whose own layout may have to be redone.
    /// </summary>
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
            Console.WriteLine("  Broiler.HTML and arrive with patches/0129; this submodule tree does not");
            Console.WriteLine("  carry it, so the rebuild/layout split below is unavailable.");
        }

        Console.WriteLine();
        Console.WriteLine($"{"page",-8} {"mutation",-20} {"1st layout",12} {"relayout",10} {"rebuild",10} {"layout",10} {"rebuild %",10}");
        Console.WriteLine(new string('-', 84));

        foreach (var row in rows)
        {
            var layout = Math.Max(0, row.RelayoutMs - row.RebuildMs);
            var share = row.RelayoutMs > 0 ? row.RebuildMs / row.RelayoutMs : 0;
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-8} {1,-20} {2,12:F2} {3,10:F2} {4,10:F2} {5,10:F2} {6,9:P1}",
                row.Page,
                row.Mutation,
                row.FirstLayoutMs,
                row.RelayoutMs,
                row.RebuildMs,
                layout,
                share));
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
