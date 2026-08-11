using System;
using System.IO;
using Broiler.HTML.Image;
using Xunit;

namespace Broiler.Wpt.Tests;

/// <summary>
/// A flex container that is a grid item is laid out again once its track has sized it, so its own
/// items stretch into the height the grid gave it rather than the auto height it was measured at.
/// </summary>
/// <remarks>
/// <para>
/// A grid item is measured before its track is sized and resized afterwards. That is fine for a
/// block — its content sits at the block-start either way — and wrong for a flex container, whose
/// line's cross size, and so how far its own items stretch, is decided from its height. Resizing
/// the box alone left a strip that was the right size holding items sized for the wrong one:
/// zero-tall boxes inside a hundred-pixel strip, which paint nothing.
/// </para>
/// <para>
/// Tested through a render rather than over the box tree because the shape is one WPT writes
/// constantly: <c>css-page/margin-boxes</c> states the margin ring as a grid of flex strips, and 26
/// of its 36 <em>reference</em> files rendered blank or nearly so before this. The markup below is
/// that shape reduced to one strip.
/// </para>
/// </remarks>
public sealed class GridItemFlexRelayoutTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    // A 300x300 grid of a 100px row, an auto row and a 100px row. The top row holds a flex strip
    // whose two items state a width and no height — so they are only visible if they stretch.
    private const string GridOfFlexStrips =
        "<!DOCTYPE html><meta charset=\"utf-8\"><style>"
        + "html,body{margin:0;padding:0}"
        + ".grid{display:grid;grid-template-columns:100px auto 100px;"
        + "grid-template-rows:100px auto 100px;width:300px;height:300px}"
        + ".strip{display:flex}"
        + "</style>"
        + "<div class=\"grid\">"
        + "<div></div>"
        + "<div class=\"strip\"><div style=\"width:50px;background:#f00\"></div>"
        + "<div style=\"width:50px;background:#00f\"></div></div>"
        + "<div></div><div></div><div></div><div></div><div></div><div></div><div></div>"
        + "</div>";

    [Fact]
    public void A_Flex_Strip_In_A_Grid_Row_Stretches_Its_Items_To_The_Track()
    {
        using var rendered = Render(GridOfFlexStrips);

        // The strip is the middle column of the first row: x 100..200, y 0..100.
        Assert.Equal((255, 0, 0), Rgb(rendered, 120, 50));
        Assert.Equal((0, 0, 255), Rgb(rendered, 170, 50));

        // ...and it stretches the whole track, not just a line's worth of it.
        Assert.Equal((255, 0, 0), Rgb(rendered, 120, 5));
        Assert.Equal((255, 0, 0), Rgb(rendered, 120, 95));
    }

    // The re-layout must not report the item where it was measured. It runs with the item still to
    // be put back in its area, and the document's running extent is what a paged render counts
    // pages with — so leaking the intermediate position doubles the page count of every reference
    // built this way.
    [Fact]
    public void The_Relayout_Does_Not_Grow_The_Documents_Extent()
    {
        using var rendered = Render(GridOfFlexStrips + "<div style=\"height:10px\"></div>");

        // Nothing below the grid and its 10px trailer: the strip's re-layout must leave no trace
        // in the extent the page count is read from.
        for (int y = 311; y < rendered.Height; y++)
            Assert.True(IsBlank(rendered, y), $"row {y} should be blank");
    }

    // The control: an item that states its own height keeps it, so this cannot have become
    // "stretch everything to its track".
    [Fact]
    public void An_Item_With_A_Height_Of_Its_Own_Keeps_It()
    {
        using var rendered = Render(GridOfFlexStrips.Replace(
            "<div style=\"width:50px;background:#f00\"></div>",
            "<div style=\"width:50px;height:20px;background:#f00\"></div>"));

        Assert.Equal((255, 0, 0), Rgb(rendered, 120, 10));
        Assert.NotEqual((255, 0, 0), Rgb(rendered, 120, 50));
    }

    private static (int R, int G, int B) Rgb(BBitmap bitmap, int x, int y)
    {
        var pixel = bitmap.GetPixel(x, y);
        return (pixel.R, pixel.G, pixel.B);
    }

    private static bool IsBlank(BBitmap bitmap, int y)
    {
        for (int x = 0; x < bitmap.Width; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.R != 255 || pixel.G != 255 || pixel.B != 255)
                return false;
        }

        return true;
    }

    private static BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-grid-flex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "grid.html");
            File.WriteAllText(file, html);
            return new WptTestRunner(400, 400).RenderHtmlFileBitmapPublic(file, dir);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
