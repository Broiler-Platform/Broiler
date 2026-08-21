using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Where a <c>::view-transition-group</c> sits when the reftests freeze it, and which author rules
/// reach it.
/// <para>
/// A group animates old→new geometry and the reftests screenshot it frozen at time 0. "Frozen at
/// time 0" is not the same as "at the old geometry": the group's <c>animation-timing-function</c> is
/// evaluated at input 0, and the <c>steps(…, jump-start)</c> family jumps before it advances, so it
/// is already part-way to the new geometry there. WPT <c>auto-name</c> and <c>auto-name-from-id</c>
/// (issue #1500 problems 7 and 8) pin exactly that with <c>steps(2, start)</c> — output 1/2 — and
/// address the rule by <c>view-transition-class</c> rather than by name.
/// </para>
/// </summary>
public class ViewTransitionGroupAnimationTests
{
    private static BBitmap Render(string html, string script)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        context.Eval(script);
        return HtmlRender.RenderToImageWithStyleSet(bridge.SerializeToHtml(), 300, 300);
    }

    private static void AssertPixel(BBitmap bitmap, int x, int y, byte r, byte g, byte b, string what)
    {
        var actual = bitmap.GetPixel(x, y);
        Assert.True(
            actual.R == r && actual.G == g && actual.B == b,
            $"{what} at ({x},{y}) was {actual.R},{actual.G},{actual.B}, expected {r},{g},{b}");
    }

    // Mirrors WPT auto-name: two column-flex items swap via `order`, so item1 travels (0,0)→(0,100)
    // and item2 travels (100,100)→(100,0). `groupRule` selects how the group is addressed and timed.
    private static string Page(string groupRule) => $$"""
<!DOCTYPE html>
<html>
<style>
  body { margin: 0; }
  div { width: 100px; height: 100px; }
  main { display: flex; flex-direction: column; }
  .item { view-transition-name: auto; view-transition-class: item; }
  main.switch #item1 { order: 2; }
  #item1 { background: green; }
  #item2 { background: yellow; position: relative; left: 100px; }
  :root { view-transition-name: none; }
  html::view-transition { background: rebeccapurple; }
  {{groupRule}}
  html::view-transition-old(*) { animation: unset; opacity: 0 }
  html::view-transition-new(*) { animation: unset; opacity: 1 }
</style>
<main id="main"><div class="item" id="item1"></div><div class="item" id="item2"></div></main>
</html>
""";

    private const string Switch =
        "document.startViewTransition(() => { document.querySelector('main').classList.add('switch'); });";

    [Theory]
    // The name-less `.item` shorthand, and the explicit `*.item` form: both select every group whose
    // captured element carries `view-transition-class: item`.
    [InlineData(".item")]
    [InlineData("*.item")]
    public void Steps_Jump_Start_Freezes_The_Group_Midway(string argument)
    {
        var html = Page(
            $"html::view-transition-group({argument}) {{ animation-timing-function: steps(2, start); }}");

        using var bitmap = Render(html, Switch);

        // steps(2, start) is 1/2 at input 0, so each group sits at the midpoint of its travel:
        // item1 at y 50..150, item2 at y 50..150 one column over.
        AssertPixel(bitmap, 50, 100, 0, 128, 0, "the green item at its midpoint");
        AssertPixel(bitmap, 150, 100, 255, 255, 0, "the yellow item at its midpoint");

        // Nothing is left at either endpoint.
        AssertPixel(bitmap, 50, 25, 102, 51, 153, "the backdrop above the green item");
        AssertPixel(bitmap, 150, 25, 102, 51, 153, "the backdrop above the yellow item");
    }

    [Fact(Timeout = 600000)]
    public void Default_Timing_Leaves_The_Group_At_The_Old_Geometry()
    {
        // linear (and every ease / cubic-bezier / jump-end steps) is 0 at input 0, so the group stays
        // where it has always been placed. This is the path almost every other test takes.
        var html = Page("html::view-transition-group(*) { animation-timing-function: linear; }");

        using var bitmap = Render(html, Switch);

        AssertPixel(bitmap, 50, 50, 0, 128, 0, "the green item at its old geometry");
        AssertPixel(bitmap, 150, 150, 255, 255, 0, "the yellow item at its old geometry");
    }

    [Fact(Timeout = 600000)]
    public void A_Class_Rule_Does_Not_Reach_A_Group_Without_That_Class()
    {
        // `view-transition-class` gates the match: a rule for `.other` must not time these groups,
        // leaving them at the old geometry.
        var html = Page(
            "html::view-transition-group(*.other) { animation-timing-function: steps(2, start); }");

        using var bitmap = Render(html, Switch);

        AssertPixel(bitmap, 50, 50, 0, 128, 0, "the green item, untimed, at its old geometry");
        AssertPixel(bitmap, 150, 150, 255, 255, 0, "the yellow item, untimed, at its old geometry");
    }
}
