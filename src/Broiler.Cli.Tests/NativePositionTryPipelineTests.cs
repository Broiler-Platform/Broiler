using System.Collections.Generic;
using System.Drawing;
using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;
using Broiler.Layout;
using Broiler.Layout.Engine;
using DomDocument = Broiler.Dom.DomDocument;
using DomElement = Broiler.Dom.DomElement;

namespace Broiler.Cli.Tests;

/// <summary>
/// End-to-end validation that the Broiler.Layout engine's native <c>@position-try</c>
/// fallback pass (Phase 5, P5.8d.2b position-try expansion) selects and applies a
/// fallback through the real parse → cascade → layout pipeline — the engine equivalent
/// of the bridge's <c>TryApplyFallback</c> pre-bake. The <c>@position-try</c> rule bodies
/// reach the engine via the out-of-band <see cref="NativeAnchorPlacement.PositionTryRules"/>
/// channel (a stylesheet at-rule never reaches the cascaded box properties).
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class NativePositionTryPipelineTests
{
    private const string Url = "file:///native-position-try.html";

    // #cb is the containing block (relative, 100x100 at the origin). #anchor is a 20x20 box
    // at (70,70) → left/top 70, right/bottom 90. #target is a 30x30 abspos box whose base
    // left/top are anchor(--a right)/anchor(--a bottom) → base border box at (90,90), which
    // overflows the 100-wide CB. The @position-try fallback flips it to the anchor's
    // opposite edges → (40,40), which fits.
    private const string Html =
        "<!DOCTYPE html><html><head><style>" +
        "body { margin: 0; }" +
        "#cb { position: relative; width: 100px; height: 100px; }" +
        "#anchor { position: absolute; left: 70px; top: 70px; width: 20px; height: 20px; anchor-name: --a; }" +
        "#target { position: absolute; width: 30px; height: 30px;" +
        " left: anchor(--a right); top: anchor(--a bottom); position-try-fallbacks: --flip; }" +
        "@position-try --flip { left: auto; right: anchor(--a left); top: auto; bottom: anchor(--a top); }" +
        "</style></head><body><div id='cb'><div id='anchor'></div><div id='target'></div></div></body></html>";

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> FlipRule =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["--flip"] = new Dictionary<string, string>
            {
                ["left"] = "auto",
                ["right"] = "anchor(--a left)",
                ["top"] = "auto",
                ["bottom"] = "anchor(--a top)",
            },
        };

    private static BoxGeometry LayoutTarget(
        bool nativeAnchor,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? rules)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, Html, Url);
        DomDocument document = bridge.GetRenderDocument();

        using var container = new HtmlContainer
        {
            Location = new PointF(0, 0),
            AvoidAsyncImagesLoading = true,
            AvoidImagesLateLoading = true,
        };
        container.SetDocumentWithStyleSet(document, baseUrl: Url);

        IReadOnlyDictionary<DomElement, BoxGeometry> geometry;
        try
        {
            NativeAnchorPlacement.Enabled = nativeAnchor;
            NativeAnchorPlacement.PositionTryRules = rules;
            geometry = container.GetLayoutGeometry(new SizeF(800, 600));
        }
        finally
        {
            NativeAnchorPlacement.Enabled = false;
            NativeAnchorPlacement.PositionTryRules = null;
        }

        var target = document.GetElementById("target");
        Assert.NotNull(target);
        Assert.True(geometry.ContainsKey(target!), "target box has no geometry");
        return geometry[target!];
    }

    [Fact]
    public void NativeFlagOn_WithRules_AppliesFallback_ToFittingPosition()
    {
        var box = LayoutTarget(nativeAnchor: true, rules: FlipRule);
        Assert.Equal(40f, box.BorderBox.Left, 1);
        Assert.Equal(40f, box.BorderBox.Top, 1);
        Assert.Equal(30f, box.BorderBox.Width, 1);
        Assert.Equal(30f, box.BorderBox.Height, 1);
    }

    [Fact]
    public void NativeFlagOn_WithoutRules_KeepsOverflowingBase()
    {
        // No @position-try rule bodies handed to the engine → the overflowing base placement
        // (90,90) is kept, proving the fallback move is the channel-fed pass's doing.
        var box = LayoutTarget(nativeAnchor: true, rules: null);
        Assert.Equal(90f, box.BorderBox.Left, 1);
        Assert.Equal(90f, box.BorderBox.Top, 1);
    }

    [Fact]
    public void NativeFlagOff_LeavesBoxUnplaced()
    {
        // Flag off → the engine cannot parse anchor() as a length, so #target sits at its
        // static/zero-inset position, not the fallback (40,40) or base (90,90).
        var box = LayoutTarget(nativeAnchor: false, rules: FlipRule);
        Assert.False(
            System.Math.Abs(box.BorderBox.Left - 40f) < 1 && System.Math.Abs(box.BorderBox.Top - 40f) < 1,
            $"box was unexpectedly at the fallback position without the native flag: {box.BorderBox}");
    }
}
