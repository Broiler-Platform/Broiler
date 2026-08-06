using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// CSSOM View §"scroll an element": the requested position is normalized to the scrolling box's
/// scrolling area, so a scroll past either end comes to rest at the end.
///
/// <c>scrollTo</c>/<c>scrollBy</c> — window and element alike — passed <c>clamp: false</c>, so
/// <c>scrollBy({top: scrollHeight})</c>, the standard "scroll to the bottom" idiom (since
/// <c>scrollHeight</c> is always at least the maximum offset), landed beyond the content and
/// painted the bare canvas. Found while diagnosing issue #1538 problem 28, whose own reference
/// performs the same scroll — so both rendered identically here and both disagreed with Chromium.
/// </summary>
public class ScrollClampingTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    /// <summary>
    /// Renders a tall page whose canvas is lightblue and whose body is lightgreen, and reports how
    /// much of each is on screen. Scrolled to the bottom the body still fills nearly the viewport;
    /// scrolled past the end there is nothing but canvas.
    /// </summary>
    private static (int Canvas, int Body) Render(string script, int w = 1024, int h = 768)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-scrollclamp-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string html =
                "<!DOCTYPE html><html><head><style>" +
                "html { background: lightblue } body { background-color: lightgreen }" +
                "</style></head><body><p>Start</p><div style='height:400vh'></div><p>End</p>" +
                "<script>" + script + "</script></body></html>";
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);

            var runner = new WptTestRunner(w, h);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            int canvas = 0, body = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (p.R == 173 && p.G == 216 && p.B == 230) canvas++;
                    else if (p.R == 144 && p.G == 238 && p.B == 144) body++;
                }

            return (canvas, body);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public void ElementScrollByPastTheEndComesToRestAtTheEnd()
    {
        var (canvas, body) = Render(
            "document.documentElement.scrollBy({top: document.documentElement.scrollHeight," +
            " behavior: 'instant'});");

        Assert.True(body > canvas, $"expected the body to still fill most of the viewport (body={body}, canvas={canvas})");
    }

    [Fact]
    public void WindowScrollByPastTheEndComesToRestAtTheEnd()
    {
        var (canvas, body) = Render("window.scrollBy(0, document.documentElement.scrollHeight);");

        Assert.True(body > canvas, $"expected the body to still fill most of the viewport (body={body}, canvas={canvas})");
    }

    [Fact]
    public void ScrollToAnAbsolutePositionPastTheEndComesToRestAtTheEnd()
    {
        var (canvas, body) = Render("window.scrollTo(0, 1e9);");

        Assert.True(body > canvas, $"expected the body to still fill most of the viewport (body={body}, canvas={canvas})");
    }

    [Fact]
    public void ANegativeScrollComesToRestAtTheTop()
    {
        // The other end of the same clamp: scrolling up from the top stays at the top, so the page
        // renders exactly as it does unscrolled.
        var scrolledUp = Render("window.scrollBy(0, -5000);");
        var unscrolled = Render(string.Empty);

        Assert.Equal(unscrolled, scrolledUp);
    }

    [Fact]
    public void AScrollWithinRangeIsUnaffected()
    {
        // Clamping must not disturb an ordinary scroll: 100px down still shows body, and a
        // different view than the top.
        var (canvas, body) = Render("window.scrollBy(0, 100);");

        Assert.True(body > 0);
        Assert.True(canvas < body);
    }
}
