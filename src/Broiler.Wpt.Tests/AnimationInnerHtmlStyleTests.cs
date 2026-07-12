using System.Linq;
using System.Reflection;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for the RF-BRIDGE-1c Phase F (F3c) follow-up in
/// <c>AnimationResolver</c>. After Attach, a <c>&lt;style&gt;</c> element's CSS can live in its
/// InnerHtml runtime state with no <c>DomText</c> child (childCount == 0) — the state the
/// full render/anchor pipeline can leave a style in after re-serialising it. The animation
/// collectors used to read the CSS by hand-walking child text nodes, which returns empty in
/// that state, so stylesheet <c>@keyframes</c> and stylesheet-declared <c>animation</c>
/// properties were silently missed (the same failure mode that broke <c>@position-try</c>).
/// The collectors now read through the canonical <c>GetStyleElementSourceText</c> accessor,
/// which covers the InnerHtml case.
///
/// The InnerHtml-only state is constructed directly (via the same reflection idiom the bridge
/// boundary-guard tests use) because it is the specific runtime state the fix targets and is
/// awkward to trigger through the public API in isolation.
/// </summary>
public sealed class AnimationInnerHtmlStyleTests
{
    // Sets an element's InnerHtml runtime state without adding a DomText child, reproducing the
    // childCount == 0 / InnerHtml-backed <style> state.
    private static void SetInnerHtmlRuntimeState(Broiler.HtmlBridge.DomBridge bridge, Broiler.Dom.DomElement el, string css)
    {
        var state = typeof(Broiler.HtmlBridge.DomBridge)
            .GetMethod("GetElementRuntimeState", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { el })!;
        state.GetType().GetProperty("InnerHtml")!.SetValue(state, css);
    }

    private static Broiler.Dom.DomElement? FindById(Broiler.Dom.DomElement el, string id)
    {
        if (el.Id == id) return el;
        foreach (var c in el.ChildNodes.OfType<Broiler.Dom.DomElement>())
            if (FindById(c, id) is { } found) return found;
        return null;
    }

    [Fact]
    public void Keyframes_AreCollected_When_StyleContent_Lives_In_InnerHtml()
    {
        // Inline animation on the target; @keyframes only reachable via the InnerHtml-backed <style>.
        const string html = @"<!DOCTYPE html>
<div id=""box"" style=""animation: slide 10s linear -10s;""></div>
<style id=""sheet""></style>";
        using var ctx = new Broiler.JavaScript.Engine.JSContext();
        var bridge = new Broiler.HtmlBridge.DomBridge();
        bridge.Attach(ctx, html, "file:///test.html");

        var sheet = FindById(bridge.DocumentElement, "sheet");
        var box = FindById(bridge.DocumentElement, "box");
        Assert.NotNull(sheet);
        Assert.NotNull(box);

        SetInnerHtmlRuntimeState(bridge, sheet!,
            "@keyframes slide { from { margin-left: 0px; } to { margin-left: 200px; } }");
        Assert.Empty(sheet!.ChildNodes); // no DomText child — the state the fix targets

        bridge.ResolveAnimationSnapshots();

        // animation-delay:-10s over a 10s duration snapshots at t=0 to the 100% ("to") keyframe.
        var style = Broiler.HtmlBridge.DomBridge.GetInlineStyleView(box!);
        Assert.True(style.TryGetValue("margin-left", out var ml) && ml == "200px",
            $"Expected margin-left:200px from @keyframes in InnerHtml <style>, got [{string.Join(", ", style.Select(kv => $"{kv.Key}:{kv.Value}"))}]");
    }

    [Fact]
    public void StylesheetAnimationProperty_IsCollected_When_StyleContent_Lives_In_InnerHtml()
    {
        // Animation declared by a stylesheet RULE (not inline) plus @keyframes — both only
        // reachable via the InnerHtml-backed <style>. Exercises CollectAnimPropsFromStyleElements
        // and CollectKeyframes together.
        const string html = @"<!DOCTYPE html>
<div id=""box""></div>
<style id=""sheet""></style>";
        using var ctx = new Broiler.JavaScript.Engine.JSContext();
        var bridge = new Broiler.HtmlBridge.DomBridge();
        bridge.Attach(ctx, html, "file:///test.html");

        var sheet = FindById(bridge.DocumentElement, "sheet");
        var box = FindById(bridge.DocumentElement, "box");
        Assert.NotNull(sheet);
        Assert.NotNull(box);

        SetInnerHtmlRuntimeState(bridge, sheet!,
            "#box { animation: slide 10s linear -10s; } " +
            "@keyframes slide { from { margin-left: 0px; } to { margin-left: 200px; } }");
        Assert.Empty(sheet!.ChildNodes);

        bridge.ResolveAnimationSnapshots();

        var style = Broiler.HtmlBridge.DomBridge.GetInlineStyleView(box!);
        Assert.True(style.TryGetValue("margin-left", out var ml) && ml == "200px",
            $"Expected margin-left:200px from stylesheet animation in InnerHtml <style>, got [{string.Join(", ", style.Select(kv => $"{kv.Key}:{kv.Value}"))}]");
    }
}
