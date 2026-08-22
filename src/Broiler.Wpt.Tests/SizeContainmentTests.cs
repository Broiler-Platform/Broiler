using System;
using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// CSS Containment 2 §3.2 (<b>size containment</b>) and CSS Sizing 4 §5
/// (<c>contain-intrinsic-size</c>): a box whose size is contained is laid out as though it had no
/// contents, and <c>contain-intrinsic-size</c> is how an author states a stand-in for the size those
/// contents would have given it.
///
/// <para>
/// Broiler parsed <c>contain</c> and acted on two of the things it says — background propagation
/// reads it, and fragmentation treats a contained box as monolithic — but nothing acted on what it
/// says about <em>size</em>, and the <c>contain-intrinsic-*</c> properties were not parsed at all.
/// Every one of the 96 assertions in WPT
/// <c>css-sizing/contain-intrinsic-size/contain-intrinsic-size-logical-003</c> (entry 21 of issue
/// #1774's top 30) therefore measured the contents the containment exists to hide.
/// </para>
///
/// <para>
/// The subtle half is the aspect ratio, and it is why several of these cases are about
/// <c>&lt;canvas&gt;</c> and <c>&lt;svg&gt;</c> rather than about <c>&lt;div&gt;</c>. Containment
/// removes an element's <b>natural</b> ratio and leaves a <b>declared</b> one alone, and the two
/// arrive at the engine looking alike: an <c>&lt;svg&gt;</c>'s <c>viewBox</c> ratio is natural and
/// goes, while the UA stylesheet's <c>aspect-ratio: attr(width) / attr(height)</c> (HTML §14.4) is a
/// declaration and stays. WPT <c>contain-size-replaced-002</c> and <c>canvas-contain-size</c> pull
/// in exactly opposite directions on that point.
/// </para>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class SizeContainmentTests
{
    private const int Width = 400;
    private const int Height = 400;

    private static BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-contain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);
            return new WptTestRunner(Width, Height).RenderHtmlFileBitmapPublic(file, dir);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static bool IsGreen(BBitmap bmp, int x, int y)
    {
        var p = bmp.GetPixel(x, y);
        return p.R < 80 && p.G > 100 && p.B < 80;
    }

    /// <summary>
    /// The bounding box of every green pixel on the canvas, as (width, height) — zero for a page
    /// with no green on it at all.
    /// </summary>
    /// <remarks>
    /// The whole canvas rather than one row and one column: each page here paints exactly one green
    /// box, but not always at the origin — an inline box sits on a text baseline, and a padded one
    /// starts inside its own padding — so a fixed sampling line misses it and reads zero, which is
    /// indistinguishable from the collapse several of these cases are testing for.
    /// </remarks>
    private static (int Width, int Height) GreenBounds(BBitmap bmp)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!IsGreen(bmp, x, y))
                    continue;

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        return maxX < 0 ? (0, 0) : (maxX - minX + 1, maxY - minY + 1);
    }

    private static int GreenWidth(BBitmap bmp) => GreenBounds(bmp).Width;

    private static int GreenHeight(BBitmap bmp) => GreenBounds(bmp).Height;

    /// <summary>
    /// A green box whose style is the case under test, holding a 40×20 block. Without containment
    /// it is 40×20; with it, whatever the containment says instead.
    /// </summary>
    private static string Box(string style) =>
        "<!DOCTYPE html><html><body style=\"margin:0\">"
        + $"<div style=\"display:inline-block; background:green; {style}\">"
        + "<div style=\"width:40px; height:20px\"></div></div>"
        + "</body></html>";

    // ---- §3.2: the contents stop counting ----

    /// <summary>Without containment the box is its contents — the control for everything below.</summary>
    [Fact(Timeout = 600000)]
    public void WithoutContainment_TheBoxIsItsContents()
    {
        var bmp = Render(Box(""));
        Assert.Equal(40, GreenWidth(bmp));
        Assert.Equal(20, GreenHeight(bmp));
    }

    /// <summary>
    /// <c>contain: size</c> with no <c>contain-intrinsic-size</c> is zero in both axes: the contents
    /// still lay out and still paint, they simply overflow a box that no longer measures them.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ContainSize_CollapsesTheBoxInBothAxes()
    {
        var bmp = Render(Box("contain:size"));
        Assert.Equal(0, GreenWidth(bmp));
        Assert.Equal(0, GreenHeight(bmp));
    }

    /// <summary><c>contain: strict</c> includes size containment; <c>contain: content</c> does not.</summary>
    [Theory]
    [InlineData("strict", 0)]
    [InlineData("size layout paint", 0)]
    [InlineData("content", 40)]
    [InlineData("layout paint style", 40)]
    [InlineData("none", 40)]
    public void OnlySizeAndStrict_ContainTheSize(string contain, int expectedWidth)
    {
        Assert.Equal(expectedWidth, GreenWidth(Render(Box($"contain:{contain}"))));
    }

    // ---- CSS Sizing 4 §5: contain-intrinsic-size stands in for the contents ----

    [Fact(Timeout = 600000)]
    public void ContainIntrinsicSize_StandsInForTheContents()
    {
        var bmp = Render(Box("contain:size; contain-intrinsic-size:100px 50px"));
        Assert.Equal(100, GreenWidth(bmp));
        Assert.Equal(50, GreenHeight(bmp));
    }

    /// <summary>One component sets both axes.</summary>
    [Fact(Timeout = 600000)]
    public void ASingleComponent_SetsBothAxes()
    {
        var bmp = Render(Box("contain:size; contain-intrinsic-size:70px"));
        Assert.Equal(70, GreenWidth(bmp));
        Assert.Equal(70, GreenHeight(bmp));
    }

    /// <summary>
    /// The grammar is <c>auto? [ none | &lt;length&gt; ]</c> per component, so the value cannot be
    /// split on whitespace: <c>auto 100px</c> is <em>one</em> component naming both axes, and
    /// <c>auto 100px auto 50px</c> is two. The <c>auto</c> keyword asks for the last remembered size
    /// and falls back to the length, which is what a render that never skipped the element sees.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheAutoKeyword_BindsForwardToItsLength()
    {
        var one = Render(Box("contain:size; contain-intrinsic-size:auto 100px"));
        Assert.Equal(100, GreenWidth(one));
        Assert.Equal(100, GreenHeight(one));

        var two = Render(Box("contain:size; contain-intrinsic-size:auto 100px auto 50px"));
        Assert.Equal(100, GreenWidth(two));
        Assert.Equal(50, GreenHeight(two));
    }

    /// <summary>
    /// <c>none</c> is the initial value and means "no stand-in", not "no containment": the axis
    /// keeps the zero an empty box has. The author width here is only so the box has something to
    /// paint with — with both axes at zero there is nothing on the canvas to measure, which is what
    /// <see cref="ContainSize_CollapsesTheBoxInBothAxes"/> covers instead.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void NoneLeavesTheAxisAtZero()
    {
        var blockOnly = Render(Box("contain:size; contain-intrinsic-size:none 50px; width:60px"));
        Assert.Equal(60, GreenWidth(blockOnly));
        Assert.Equal(50, GreenHeight(blockOnly));

        var inlineOnly = Render(Box("contain:size; contain-intrinsic-size:60px none; height:35px"));
        Assert.Equal(60, GreenWidth(inlineOnly));
        Assert.Equal(35, GreenHeight(inlineOnly));
    }

    /// <summary>The physical longhands, set on their own rather than through the shorthand.</summary>
    [Fact(Timeout = 600000)]
    public void ThePhysicalLonghands_SetOneAxisEach()
    {
        var bmp = Render(Box("contain:size; contain-intrinsic-width:80px; contain-intrinsic-height:30px"));
        Assert.Equal(80, GreenWidth(bmp));
        Assert.Equal(30, GreenHeight(bmp));
    }

    /// <summary>
    /// The flow-relative longhands in a horizontal writing mode, where inline is the width.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheLogicalLonghands_MapToTheWritingModesAxes()
    {
        var bmp = Render(Box("contain:size; contain-intrinsic-inline-size:80px; contain-intrinsic-block-size:30px"));
        Assert.Equal(80, GreenWidth(bmp));
        Assert.Equal(30, GreenHeight(bmp));
    }

    /// <summary>
    /// And in a vertical one, where they swap: the inline axis runs down the page, so
    /// <c>contain-intrinsic-inline-size</c> is the <em>height</em>. Eight of
    /// <c>contain-intrinsic-size-logical-003</c>'s cases are this, and mapping the value the way
    /// <c>min-width</c> is mapped got them transposed — the vertical-flow prototype lays such a box
    /// out in a logical frame and rotates it afterwards, so the value has to arrive in frame terms.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void InAVerticalWritingMode_TheLogicalLonghandsSwapAxes()
    {
        var bmp = Render(Box(
            "writing-mode:vertical-lr; contain:size;"
            + " contain-intrinsic-inline-size:80px; contain-intrinsic-block-size:30px"));

        Assert.Equal(30, GreenWidth(bmp));
        Assert.Equal(80, GreenHeight(bmp));
    }

    /// <summary>An author's own <c>width</c>/<c>height</c> is the box's own size and still wins.</summary>
    [Fact(Timeout = 600000)]
    public void AnAuthorSize_WinsOverTheContainedIntrinsicSize()
    {
        var bmp = Render(Box("contain:size; contain-intrinsic-size:100px 50px; width:25px; height:75px"));
        Assert.Equal(25, GreenWidth(bmp));
        Assert.Equal(75, GreenHeight(bmp));
    }

    // ---- §3.2: which boxes it applies to ----

    /// <summary>
    /// Size containment has no effect on a non-atomic inline box: its size is the line's to decide,
    /// not its own, so zeroing it would remove a size the box never owned.
    /// </summary>
    /// <remarks>
    /// Measured through the block the inline sits in rather than off the inline itself: what would
    /// go wrong is the inline reporting a zero intrinsic width, and the shrink-to-fit block around
    /// it collapsing with it. So the green is on the block, and 40px of it says the span's contents
    /// still counted.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void ANonAtomicInline_IsUnaffected()
    {
        Assert.Equal(40, GreenWidth(Render(
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<div style=\"display:inline-block; background:green\">"
            + "<span style=\"display:inline; contain:size\">"
            + "<span style=\"display:inline-block; width:40px; height:20px\"></span></span>"
            + "</div></body></html>")));
    }

    /// <summary>
    /// A <em>replaced</em> element is the exception that costs the most to get wrong: its computed
    /// <c>display</c> is <c>inline</c> — that is the UA value for <c>&lt;img&gt;</c> and
    /// <c>&lt;svg&gt;</c> — but the box it generates is atomic, so containment does apply. Testing
    /// the keyword alone declined containment on precisely the elements
    /// <c>css-contain/contain-size-replaced-*</c> is written about.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AReplacedInlineElement_IsContained()
    {
        // Without containment the viewBox ratio makes this 100x100; with it, 100x0 plus the padding.
        var bmp = Render(
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<svg width=\"100\" viewBox=\"0 0 50 50\" style=\"background:green; padding:20px 0; contain:size\">"
            + "<rect fill=\"red\" width=\"50\" height=\"50\"/></svg>"
            + "</body></html>");

        Assert.Equal(40, GreenHeight(bmp));
    }

    // ---- the ratio: natural goes, declared stays ----

    /// <summary>
    /// HTML §14.4: the <c>width</c>/<c>height</c> content attributes map to
    /// <c>aspect-ratio: attr(width) / attr(height)</c>, a UA stylesheet <em>declaration</em>, so a
    /// contained element keeps it and a definite inline size still settles the block axis. WPT
    /// <c>css-flexbox/canvas-contain-size</c> and <c>contain-size-replaced-007</c> both assert this
    /// in as many words.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void APresentationalAttributeRatio_SurvivesContainment()
    {
        Assert.Equal(100, GreenHeight(Render(
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<canvas width=20 height=20 style=\"contain:size; background:green; width:100px\"></canvas>"
            + "</body></html>")));
    }

    /// <summary>
    /// SVG 2 §8.2: a <c>viewBox</c> states the element's <em>natural</em> ratio, which containment
    /// removes — the mirror image of the case above, and the reason the two cannot share one rule.
    /// Broiler records the viewBox ratio into <c>aspect-ratio</c> during style resolution, so by
    /// layout time a natural ratio and a declared one look alike; comparing the recorded value back
    /// against the viewBox is what tells them apart.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AViewBoxRatio_DoesNotSurviveContainment()
    {
        Assert.Equal(0, GreenHeight(Render(
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<svg width=\"100\" viewBox=\"0 0 50 50\" style=\"background:green; contain:size\"></svg>"
            + "</body></html>")));
    }

    /// <summary>An author's own <c>aspect-ratio</c> is a declaration too, and survives.</summary>
    [Fact(Timeout = 600000)]
    public void AnAuthorAspectRatio_SurvivesContainment()
    {
        Assert.Equal(50, GreenHeight(Render(Box(
            "contain:size; aspect-ratio:2/1; width:100px; background:green"))));
    }
}
