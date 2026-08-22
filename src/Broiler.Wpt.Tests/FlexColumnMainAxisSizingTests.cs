using System;
using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// CSS Flexbox §9.2, §9.4 and §9.7 along a <c>column</c> container's main (block) axis — the
/// block-axis twin of <see cref="FlexAutomaticMinimumSizeTests"/> — and CSS2.1 §10.6.4's
/// contribution to it: what makes a column container's main size <em>definite</em> in the first
/// place.
///
/// <para>
/// A column container's items stack through ordinary block flow here, and the pass that resizes
/// that stack afterwards implemented only half of §9.7. <c>flex-basis</c> was never read in the
/// block axis — block flow has no notion of it — so an item whose only stated size was a
/// <c>flex-basis</c> came out at the height of its own content. And nothing shrank at all, on the
/// stated grounds that §4.5's automatic minimum size did not exist in this axis to floor a shrink
/// with; since <c>flex-shrink</c> defaults to <c>1</c>, that is every column flex item on the web
/// asking to shrink and not shrinking.
/// </para>
///
/// <para>
/// Both are gated on the container having a definite main size, which was read from its
/// <c>height</c> declaration alone. An out-of-flow box with <c>height: auto</c> and both
/// <c>top</c> and <c>bottom</c> set has no such declaration and a perfectly definite used height —
/// §10.6.4 solves for it — and <c>position: absolute; inset: 0</c> is how a full-viewport app
/// shell is written, so the most common definite column container there is was treated as
/// content-sized. That is the shape of WPT <c>css-flexbox/percentage-heights-002</c> (issue #1774,
/// 40.9%), which rendered the red backdrop it says you should not see.
/// </para>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class FlexColumnMainAxisSizingTests
{
    private const int Width = 200;
    private const int Height = 400;

    private static BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-flexcol-" + Guid.NewGuid().ToString("N"));
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

    private static bool IsBlue(BBitmap bmp, int x, int y)
    {
        var p = bmp.GetPixel(x, y);
        return p.R < 80 && p.G < 80 && p.B > 180;
    }

    private static bool IsGreen(BBitmap bmp, int x, int y)
    {
        var p = bmp.GetPixel(x, y);
        return p.R < 80 && p.G > 100 && p.B < 80;
    }

    /// <summary>
    /// The height of the band of <paramref name="matches"/> pixels down column
    /// <paramref name="x"/> — the item's used border-box height, measured off the canvas rather
    /// than read from the box tree.
    /// </summary>
    /// <remarks>
    /// First and last matching row rather than the length of an unbroken run: a band carrying
    /// content has other pixels sitting on it. Measuring the band's <em>height</em> rather than
    /// its bottom edge matters as soon as it does not start at the top of the canvas, which is
    /// every inset-positioned case below.
    /// </remarks>
    private static int Extent(BBitmap bmp, Func<BBitmap, int, int, bool> matches, int x = 2)
    {
        int first = -1, last = -1;

        for (int y = 0; y < Height; y++)
        {
            if (!matches(bmp, x, y))
                continue;

            if (first < 0)
                first = y;

            last = y;
        }

        return first < 0 ? 0 : last - first + 1;
    }

    private static int BlueExtent(BBitmap bmp) => Extent(bmp, IsBlue);

    /// <summary>
    /// A 100px-tall column flex container holding one blue item, whose style is the case under
    /// test. The item is 10px wide so nothing else can be confused with it on the column sampled.
    /// </summary>
    private static string Column(string itemStyle, string itemContent = "") =>
        "<!DOCTYPE html><html><body style=\"margin:0\">"
        + "<div style=\"display:flex; flex-direction:column; width:30px; height:100px\">"
        + $"<div style=\"width:10px; background:blue; {itemStyle}\">{itemContent}</div>"
        + "</div></body></html>";

    /// <summary>
    /// The same, but only 30px tall and holding 60px of content — so §9.7's target for the item
    /// falls <em>below</em> its content size and §4.5's floor is what decides the answer.
    /// </summary>
    private static string ShortColumn(string itemStyle) =>
        "<!DOCTYPE html><html><body style=\"margin:0\">"
        + "<div style=\"display:flex; flex-direction:column; width:30px; height:30px\">"
        + $"<div style=\"width:10px; background:blue; {itemStyle}\">"
        + "<div style=\"width:10px; height:60px\"></div></div>"
        + "</div></body></html>";

    // ---- §9.2 step 3: flex-basis names the item's block size in a column ----

    /// <summary>
    /// The base case: <c>flex-basis</c> is the item's flex base size in a column container, and
    /// block flow — which produced the stack this pass resizes — knows nothing about it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void FlexBasis_SizesAColumnItemThatDeclaresNoHeight()
    {
        Assert.Equal(60, BlueExtent(Render(Column("flex:0 0 60px"))));
    }

    /// <summary>
    /// A percentage <c>flex-basis</c> resolves against the container's main size — the axis the
    /// container is a column along, not its inline size.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void APercentageFlexBasis_ResolvesAgainstTheContainersMainSize()
    {
        Assert.Equal(40, BlueExtent(Render(Column("flex:0 0 40%"))));
    }

    /// <summary>An item's own <c>height</c> is its base size when <c>flex-basis</c> is auto.</summary>
    [Fact(Timeout = 600000)]
    public void AnItemThatFits_IsUntouched()
    {
        Assert.Equal(40, BlueExtent(Render(Column("height:40px"))));
    }

    // ---- §9.7: shrinking, the half that was left out ----

    /// <summary>
    /// The case the shrink half is about: an item taller than its container, everything else
    /// initial. <c>flex-shrink</c> is <c>1</c> by default, so it must come down to the container's
    /// 100px rather than overflow it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnItemTallerThanItsContainer_ShrinksToFit()
    {
        Assert.Equal(100, BlueExtent(Render(Column("height:300px"))));
    }

    /// <summary><c>flex-shrink: 0</c> opts out, so the item keeps its height and overflows.</summary>
    [Fact(Timeout = 600000)]
    public void FlexShrinkZero_KeepsTheSpecifiedHeight()
    {
        Assert.Equal(300, BlueExtent(Render(Column("flex-shrink:0; height:300px"))));
    }

    /// <summary>
    /// §9.7 step 2 scales each shrink factor by the item's base size, so a large item gives up
    /// proportionally more than a small one with the same <c>flex-shrink</c>. 120 and 60 in a
    /// 100px container overflow by 80, and the scaled shares are 120/180 and 60/180 of it: they
    /// land at 66⅔ and 33⅓, which together fill the container exactly.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ShrinkingIsScaledByTheBaseSize()
    {
        var bmp = Render(
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<div style=\"display:flex; flex-direction:column; width:30px; height:100px\">"
            + "<div style=\"width:10px; height:120px; background:blue\"></div>"
            + "<div style=\"width:10px; height:60px; background:green\"></div>"
            + "</div></body></html>");

        Assert.InRange(BlueExtent(bmp), 66, 67);
        Assert.InRange(Extent(bmp, IsGreen), 33, 34);
    }

    // ---- §4.5: the automatic minimum size, which is what makes shrinking safe ----

    /// <summary>
    /// An undeclared <c>min-height</c> is <c>auto</c> on a flex item, and floors it at its own
    /// content. Without this floor a container smaller than its content squashes everything in it
    /// towards nothing, which is why turning shrinking on needed it first.
    /// </summary>
    /// <remarks>
    /// The container is 30px here, below the 60px the item's content measures — the floor only
    /// says anything once §9.7's target falls under it, and against the 100px container the other
    /// cases use it never does.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void AnItem_DoesNotShrinkBelowItsContent()
    {
        Assert.Equal(60, BlueExtent(Render(ShortColumn("height:300px"))));
    }

    /// <summary>
    /// <c>min-height: 0</c> is the idiom for opting out of that floor, and has to be told apart
    /// from an undeclared minimum — which shares its computed value, since <c>min-height</c> is
    /// <c>0</c> by default.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnExplicitZeroMinHeight_OptsOutOfTheAutomaticMinimum()
    {
        Assert.Equal(30, BlueExtent(Render(ShortColumn("min-height:0; height:300px"))));
    }

    /// <summary>
    /// §4.5 gives no automatic minimum to an item that scrolls its own overflow.
    /// <c>flex: 1; overflow: auto</c> is the scrolling pane of most column layouts on the web, and
    /// flooring it at its content is what makes such a pane push its container open rather than
    /// scroll.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AScrollContainer_HasNoAutomaticMinimum()
    {
        Assert.Equal(30, BlueExtent(Render(ShortColumn("overflow:auto; height:300px"))));
    }

    /// <summary>
    /// An explicit <c>min-height</c> larger than the container still floors the shrink — the
    /// automatic branch is not the only one.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnExplicitMinHeight_FloorsTheShrink()
    {
        Assert.Equal(150, BlueExtent(Render(Column("min-height:150px; height:300px"))));
    }

    /// <summary>
    /// §9.7 step 4 clamps every item's target, not only a shrunk one: an item that takes no share
    /// of the free space still has a maximum. WPT <c>flex-item-max-height-min-content</c> is this
    /// shape — a <c>flex-basis: 200px</c> item capped at the 60px its content measures.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void MaxHeightMinContent_CapsALargerFlexBasis()
    {
        Assert.Equal(60, BlueExtent(Render(
            Column("max-height:min-content; flex:0 0 200px",
                   "<div style=\"width:10px; height:60px\"></div>"))));
    }

    /// <summary>
    /// The mirror of the case above, and what WPT <c>flex-minimum-height-flex-items-011</c> pins:
    /// a <c>flex-basis: 0</c> item in a container far smaller than its content is floored at that
    /// content, in a direction §9.7 never distributes into.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AZeroFlexBasis_IsFlooredAtTheItemsContent()
    {
        Assert.Equal(100, BlueExtent(Render(
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<div style=\"display:flex; flex-direction:column; width:30px; height:10px\">"
            + "<div style=\"width:10px; flex-basis:0; background:blue\">"
            + "<div style=\"width:10px; height:100px\"></div></div>"
            + "</div></body></html>")));
    }

    // ---- CSS2.1 §10.6.4: an inset pair is a definite main size ----

    /// <summary>
    /// A <c>position: absolute</c> container with <c>height: auto</c> and both <c>top</c> and
    /// <c>bottom</c> set: §10.6.4 solves its used height, so its items flex against that. This is
    /// <c>percentage-heights-002</c> in miniature — the item's <c>flex-basis: 100%</c> and the
    /// 20%-tall band above it add up to more than the container, and both shrink into it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnInsetSizedAbsposContainer_HasADefiniteMainSize()
    {
        var bmp = Render(
            "<!DOCTYPE html><html><body style=\"margin:0; position:relative; width:200px; height:300px\">"
            + "<div style=\"position:absolute; top:0; bottom:0; left:0; right:0;"
            + " display:flex; flex-direction:column\">"
            + "<div style=\"height:20%; background:green\"></div>"
            + "<div style=\"flex-basis:100%; background:blue\"></div>"
            + "</div></body></html>");

        // 20% and 100% of 300 is 60 + 300 = 360, overflowing by 60; the scaled shrink shares are
        // 10 and 50, so the bands are 50 and 250 and together fill the container exactly.
        Assert.Equal(50, Extent(bmp, IsGreen));
        Assert.Equal(250, BlueExtent(bmp));
    }

    /// <summary>
    /// The same definiteness, seen through §10.5 instead: a percentage <c>height</c> inside an
    /// inset-sized box resolves against the height its insets solved for rather than falling back
    /// to <c>auto</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void APercentageHeight_ResolvesAgainstAnInsetSizedContainingBlock()
    {
        Assert.Equal(75, BlueExtent(Render(
            "<!DOCTYPE html><html><body style=\"margin:0; position:relative; width:200px; height:400px\">"
            + "<div style=\"position:absolute; top:50px; bottom:50px; left:0; right:0\">"
            + "<div style=\"width:10px; height:25%; background:blue\"></div>"
            + "</div></body></html>")));
    }

    /// <summary>
    /// The insets have to actually solve for something: one of them left <c>auto</c> leaves the
    /// block size content-derived, and a percentage inside it goes back to <c>auto</c> (§10.5).
    /// </summary>
    [Fact(Timeout = 600000)]
    public void OneInsetAlone_LeavesTheBlockSizeIndefinite()
    {
        Assert.Equal(0, BlueExtent(Render(
            "<!DOCTYPE html><html><body style=\"margin:0; position:relative; width:200px; height:400px\">"
            + "<div style=\"position:absolute; top:50px; left:0; right:0\">"
            + "<div style=\"width:10px; height:25%; background:blue\"></div>"
            + "</div></body></html>")));
    }

    /// <summary>§10.7 clamps the §10.6.4 solution, and the clamped value is the definite one.</summary>
    [Fact(Timeout = 600000)]
    public void AMaxHeight_ClampsTheInsetDerivedBlockSize()
    {
        Assert.Equal(50, BlueExtent(Render(
            "<!DOCTYPE html><html><body style=\"margin:0; position:relative; width:200px; height:400px\">"
            + "<div style=\"position:absolute; top:0; bottom:0; left:0; right:0; max-height:100px\">"
            + "<div style=\"width:10px; height:50%; background:blue\"></div>"
            + "</div></body></html>")));
    }

    // ---- a vertical writing mode crosses the axes this pass reads ----

    /// <summary>
    /// <c>column</c> names the <em>block</em> axis, so under <c>vertical-lr</c> the main axis is
    /// horizontal and every number this pass takes from the height belongs to the cross axis.
    /// Growing on the wrong axis was merely inert; shrinking on it is not, and WPT
    /// <c>flexbox-column-row-gap-002</c>'s <c>vertical-lr</c> container squashed all six of its
    /// items to fit a main size that is not its main size. Such a container is left to block flow.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AVerticalWritingModeContainer_IsLeftToBlockFlow()
    {
        Assert.Equal(300, BlueExtent(Render(
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<div style=\"display:flex; flex-direction:column; writing-mode:vertical-lr;"
            + " width:30px; height:100px\">"
            + "<div style=\"width:10px; height:300px; background:blue\"></div>"
            + "</div></body></html>")));
    }
}
