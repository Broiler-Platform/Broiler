using System;
using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// CSS 2.1 §17.2.1 anonymous table objects: a row group may contain only rows, so a non-row child
/// has to be wrapped in an anonymous <c>table-row</c> (and an anonymous cell inside it).
/// <c>CssLayoutEngineTable</c> generated those wrappers for children sitting directly under the
/// table, but not for children of a row group — and its collection loop takes a group's children
/// only when their display is <c>table-row</c>, so every other child was silently dropped: never
/// laid out, never painted, and contributing no height to the table.
///
/// <para>Regression for WPT <c>css/css-page/monolithic-overflow-011-print</c> (2.3% pixel match
/// against the Chromium reference), which puts a 350vh block straight inside a
/// <c>table-row-group</c>. The block vanished and the yellow table collapsed to its text
/// line.</para>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class TableRowGroupAnonymousRowTests
{
    private const int Size = 200;

    private static BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-tablerowgroup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);
            return new WptTestRunner(Size, Size).RenderHtmlFileBitmapPublic(file, dir);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact(Timeout = 600000)]
    public void NonRowChildOfRowGroup_IsWrappedAndPainted()
    {
        // A tall hotpink block directly inside a table-row-group. It must paint, and its height
        // must reach the table so the yellow background fills the viewport behind it.
        const string Html = """
<!DOCTYPE html><meta charset="utf-8"><style>body{margin:0}</style>
<div style="display:table; width:100%; background:yellow;">
  <div style="display:table-row-group;">
    <div style="height:300vh; width:50px; background:hotpink;"></div>
  </div>
</div>
""";
        using var bmp = Render(Html);

        // The wrapped block paints its own column...
        var inside = bmp.GetPixel(10, Size / 2);
        Assert.True(inside is { R: 255, G: 105, B: 180 },
            $"(10,{Size / 2}) was rgb({inside.R},{inside.G},{inside.B}) — the row group's non-row child " +
            "was dropped instead of being wrapped in an anonymous row.");

        // ...and the table grew to contain it, so the yellow background reaches the bottom.
        var beside = bmp.GetPixel(Size - 5, Size - 5);
        Assert.True(beside is { R: 255, G: 255, B: 0 },
            $"({Size - 5},{Size - 5}) was rgb({beside.R},{beside.G},{beside.B}) — the table did not take " +
            "the wrapped child's height.");
    }

    [Fact(Timeout = 600000)]
    public void ProperRowsInRowGroup_AreUnaffected()
    {
        // The ordinary shape must be untouched: real rows are not re-wrapped or reordered.
        const string Html = """
<!DOCTYPE html><meta charset="utf-8"><style>body{margin:0}</style>
<div style="display:table; width:100%;">
  <div style="display:table-row-group;">
    <div style="display:table-row"><div style="display:table-cell; height:40px; background:hotpink;"></div></div>
    <div style="display:table-row"><div style="display:table-cell; height:40px; background:yellow;"></div></div>
  </div>
</div>
""";
        using var bmp = Render(Html);

        var first = bmp.GetPixel(10, 20);
        var second = bmp.GetPixel(10, 60);
        Assert.True(first is { R: 255, G: 105, B: 180 },
            $"(10,20) was rgb({first.R},{first.G},{first.B}) — expected the first row.");
        Assert.True(second is { R: 255, G: 255, B: 0 },
            $"(10,60) was rgb({second.R},{second.G},{second.B}) — expected the second row below the first.");
    }
}
