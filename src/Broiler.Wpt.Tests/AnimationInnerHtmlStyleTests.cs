using System.Linq;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for the <c>AnimationResolver</c> stylesheet collectors: <c>@keyframes</c> and
/// stylesheet-declared <c>animation</c> properties must be collected from a <c>&lt;style&gt;</c>
/// element's CSS (read through the canonical <c>GetStyleElementSourceText</c> accessor, the same
/// source the cascade reads), not just from inline styles.
///
/// Phase 4 item 3 removed the parallel <c>InnerHtml</c> runtime-state string: a <c>&lt;style&gt;</c>
/// element's CSS is always a canonical <see cref="Broiler.Dom.DomText"/> child, so these tests place
/// the CSS directly in the parsed <c>&lt;style&gt;</c> element (its production shape) rather than
/// fabricating the former InnerHtml-backed, childless state via reflection.
/// </summary>
public sealed class AnimationInnerHtmlStyleTests
{
    private static Broiler.Dom.DomElement? FindById(Broiler.Dom.DomElement el, string id)
    {
        if (el.Id == id) return el;
        foreach (var c in el.ChildNodes.OfType<Broiler.Dom.DomElement>())
            if (FindById(c, id) is { } found) return found;
        return null;
    }

    [Fact]
    public void Keyframes_AreCollected_From_StyleElement_TextContent()
    {
        // Inline animation on the target; @keyframes only reachable via the <style> element's CSS.
        const string html = @"<!DOCTYPE html>
<div id=""box"" style=""animation: slide 10s linear -10s;""></div>
<style id=""sheet"">@keyframes slide { from { margin-left: 0px; } to { margin-left: 200px; } }</style>";
        using var ctx = new Broiler.JavaScript.Engine.JSContext();
        var bridge = new Broiler.HtmlBridge.DomBridge();
        bridge.Attach(ctx, html, "file:///test.html");

        var sheet = FindById(bridge.DocumentElement, "sheet");
        var box = FindById(bridge.DocumentElement, "box");
        Assert.NotNull(sheet);
        Assert.NotNull(box);
        // The CSS is a canonical DomText child — the production shape the collectors read.
        Assert.Contains(sheet!.ChildNodes, c => c is Broiler.Dom.DomText);

        bridge.ResolveAnimationSnapshots();

        // animation-delay:-10s over a 10s duration snapshots at t=0 to the 100% ("to") keyframe.
        var style = bridge.GetInlineStyleView(box!);
        Assert.True(style.TryGetValue("margin-left", out var ml) && ml == "200px",
            $"Expected margin-left:200px from @keyframes in <style>, got [{string.Join(", ", style.Select(kv => $"{kv.Key}:{kv.Value}"))}]");
    }

    [Fact]
    public void StylesheetAnimationProperty_IsCollected_From_StyleElement_TextContent()
    {
        // Animation declared by a stylesheet RULE (not inline) plus @keyframes — both from the
        // <style> element's CSS. Exercises CollectAnimPropsFromStyleElements and CollectKeyframes.
        const string html = @"<!DOCTYPE html>
<div id=""box""></div>
<style id=""sheet"">#box { animation: slide 10s linear -10s; } @keyframes slide { from { margin-left: 0px; } to { margin-left: 200px; } }</style>";
        using var ctx = new Broiler.JavaScript.Engine.JSContext();
        var bridge = new Broiler.HtmlBridge.DomBridge();
        bridge.Attach(ctx, html, "file:///test.html");

        var sheet = FindById(bridge.DocumentElement, "sheet");
        var box = FindById(bridge.DocumentElement, "box");
        Assert.NotNull(sheet);
        Assert.NotNull(box);
        Assert.Contains(sheet!.ChildNodes, c => c is Broiler.Dom.DomText);

        bridge.ResolveAnimationSnapshots();

        var style = bridge.GetInlineStyleView(box!);
        Assert.True(style.TryGetValue("margin-left", out var ml) && ml == "200px",
            $"Expected margin-left:200px from stylesheet animation in <style>, got [{string.Join(", ", style.Select(kv => $"{kv.Key}:{kv.Value}"))}]");
    }
}
