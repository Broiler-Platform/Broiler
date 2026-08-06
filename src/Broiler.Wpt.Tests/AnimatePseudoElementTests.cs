using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for <c>element.animate()</c>'s two gaps behind issue #1538 problem 11 (WPT
/// <c>css/css-pseudo/backdrop-animate-002</c>): the property-indexed keyframe form, and animations
/// that target a pseudo-element.
///
/// The test animates <c>::backdrop</c> to a 10%-opacity green with
/// <c>{opacity: [0.1, 0.1], backgroundColor: ["green", "green"]}</c> and got the UA modal scrim.
/// Its own reference writes the same declarations as CSS and already rendered correctly, which is
/// what said the gap was the API rather than the pseudo-element.
/// </summary>
public class AnimatePseudoElementTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private static (int W, int H, System.Func<int, int, (int R, int G, int B)> At) RenderPage(
        string body, int w = 200, int h = 200)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-animpseudo-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, "<!DOCTYPE html><html><head></head><body style='margin:0'>" + body + "</body></html>");

            var runner = new WptTestRunner(w, h);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            var pixels = new (int, int, int)[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    pixels[x, y] = (p.R, p.G, p.B);
                }

            return (w, h, (x, y) => pixels[x, y]);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>The colour in a corner, well away from the centred dialog box.</summary>
    private static (int R, int G, int B) Backdrop(string script)
    {
        var (_, _, at) = RenderPage(
            "<dialog id=target>x</dialog><script>" + script + "</script>");
        return at(4, 4);
    }

    [Fact]
    public void AnimatingBackdropMatchesTheEquivalentStyleRule()
    {
        // backdrop-animate-002 against its own reference, reduced to the two colours that decide it.
        var animated = Backdrop(
            "const t = document.getElementById('target'); t.showModal();" +
            "t.animate({opacity: [0.1, 0.1], backgroundColor: ['green', 'green']}," +
            " {pseudoElement: '::backdrop', duration: Infinity});");

        var (_, _, styled) = RenderPage(
            "<style>#target::backdrop { opacity: 0.1; background-color: green }</style>" +
            "<dialog id=target>x</dialog>" +
            "<script>document.getElementById('target').showModal();</script>");

        Assert.Equal(styled(4, 4), animated);
        // Green at 10% over white, not the UA scrim (229,229,229) and not solid green.
        Assert.Equal((229, 242, 229), animated);
    }

    [Fact]
    public void PropertyIndexedKeyframesAreParsed()
    {
        // The array-of-keyframes form was the only one understood, so this shape resolved to zero
        // keyframes and the whole animation was inert. Element-targeted, so it also pins that the
        // parsing fix is not specific to pseudo-elements.
        var (_, _, at) = RenderPage(
            "<div id=box style='width:100px;height:100px;background:red'></div>" +
            "<script>document.getElementById('box')" +
            ".animate({backgroundColor: ['green', 'green']}, {duration: 1000});</script>");

        Assert.Equal((0, 128, 0), at(50, 50));
    }

    [Fact]
    public void ASinglePropertyValueIsTheFinalKeyframe()
    {
        // Web Animations distributes a property's values evenly and treats a list of one as the
        // effect's end value, so it applies at the snapshot rather than being dropped.
        var (_, _, at) = RenderPage(
            "<div id=box style='width:100px;height:100px;background:red'></div>" +
            "<script>document.getElementById('box')" +
            ".animate({backgroundColor: 'green'}, {duration: 1000});</script>");

        Assert.Equal((0, 128, 0), at(50, 50));
    }

    [Fact]
    public void AnAnimationWithNoPseudoElementStillTargetsTheElement()
    {
        // The pseudo branch must not capture the ordinary case: without the option the values still
        // bake onto the element's own inline style.
        var (_, _, at) = RenderPage(
            "<div id=box style='width:100px;height:100px;background:red'></div>" +
            "<script>document.getElementById('box')" +
            ".animate([{backgroundColor: 'green'}, {backgroundColor: 'green'}], {duration: 1000});</script>");

        Assert.Equal((0, 128, 0), at(50, 50));
    }

    [Fact]
    public void AnimatingOneElementsBackdropDoesNotReachAnother()
    {
        // The bake is keyed per element and per pseudo; a second dialog's backdrop keeps the UA
        // scrim. Only the second dialog is opened, so what shows is its own backdrop.
        var backdrop = Backdrop(
            "const t = document.getElementById('target');" +
            "const other = document.createElement('dialog'); document.body.appendChild(other);" +
            "t.animate({backgroundColor: ['green', 'green']}," +
            " {pseudoElement: '::backdrop', duration: Infinity});" +
            "other.showModal();");

        Assert.Equal((229, 229, 229), backdrop);
    }
}
