using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for list-item marker painting. <c>CssBox.CreateListItemBox</c> lays out a
/// generated marker (bullet / number) for every <c>display:list-item</c> element, but the marker
/// box is not a member of <c>box.Boxes</c>, so before the fix the fragment builder never emitted
/// it and no list ever painted a marker. <see cref="Broiler.Layout.IR.FragmentTreeBuilder"/> now
/// projects the marker word into a dedicated line fragment.
///
/// The tests render a list and assert dark ink appears in the marker gutter (to the left of the
/// list content), and that <c>list-style-type:none</c> paints no marker there — so the fix cannot
/// regress into drawing a marker where the author suppressed it.
/// </summary>
public class ListItemMarkerRenderTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    // A list item indented well clear of the left edge so the marker gutter is unambiguous.
    private const string MarkerHtml =
        "<!DOCTYPE html><meta charset=\"utf-8\">" +
        "<style>body{margin:0} ul{margin:0;padding-left:80px} li{font:20px monospace;color:#000}</style>" +
        "<ul><li>item</li></ul>";

    private const string NoMarkerHtml =
        "<!DOCTYPE html><meta charset=\"utf-8\">" +
        "<style>body{margin:0} ul{margin:0;padding-left:80px;list-style-type:none} li{font:20px monospace;color:#000}</style>" +
        "<ul><li>item</li></ul>";

    // Count dark pixels inside the marker gutter: the horizontal band left of the content box
    // (x in [40,74]) — the marker sits at contentLeft - width - 5, i.e. around x≈60 for an 80px indent.
    private static int GutterInk(BBitmap bmp)
    {
        int count = 0;
        for (int y = 0; y < 60; y++)
            for (int x = 40; x < 75; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.R < 100 && p.G < 100 && p.B < 100)
                    count++;
            }
        return count;
    }

    private static int RenderGutterInk(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-list-marker-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "list.html");
            File.WriteAllText(file, html);
            var runner = new WptTestRunner(400, 200);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);
            return GutterInk(bmp);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void ListItem_PaintsMarkerInGutter()
    {
        Assert.True(RenderGutterInk(MarkerHtml) > 0,
            "no marker ink painted in the gutter for a display:list-item element.");
    }

    [Fact]
    public void ListStyleNone_PaintsNoMarker()
    {
        Assert.True(RenderGutterInk(NoMarkerHtml) == 0,
            "a marker was painted despite list-style-type:none.");
    }
}
