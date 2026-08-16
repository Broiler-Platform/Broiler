using System;
using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// CSS 2.1 §17.2.1 <em>generate missing parents</em> — the mirror image of
/// <see cref="TableRowGroupAnonymousRowTests"/>, which covers the <em>missing children</em>
/// half of the same section.
///
/// <para><c>CssLayoutEngineTable</c> only ever runs on a box that is already a table, so it
/// could not see a <c>table-row</c>, row group or <c>table-cell</c> sitting outside the table
/// box it requires. Such a box is neither <c>IsBlock</c> nor <c>IsInline</c>, so block layout
/// walked straight past it: it never laid out, never painted, and contributed no height.</para>
///
/// <para>Regression for WPT's <c>css/CSS2/tables/table-anonymous-objects-*</c> family (103 of
/// its members were failing), whose green "there should be no red" layer is built almost
/// entirely out of bare <c>display: table-row-group</c> / <c>table-row</c> spans with no table
/// around them — so the layer that is supposed to cover the red one painted nothing at all.</para>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class AnonymousTableBoxParentTests
{
    private const int Size = 200;

    private static BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-anontableparent-" + Guid.NewGuid().ToString("N"));
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

    private static void AssertPixel(BBitmap bmp, int x, int y, byte r, byte g, byte b, string because)
    {
        var actual = bmp.GetPixel(x, y);
        Assert.True(actual.R == r && actual.G == g && actual.B == b,
            $"({x},{y}) was rgb({actual.R},{actual.G},{actual.B}), expected rgb({r},{g},{b}) — {because}");
    }

    [Fact]
    public void RowWithoutATable_IsWrappedAndPainted()
    {
        // A table-row whose parent is a plain block. §17.2.1 generates an anonymous table
        // around it; without one the row — and both its cells — were dropped entirely.
        const string Html = """
<!DOCTYPE html><meta charset="utf-8"><style>body{margin:0}</style>
<div>
  <div style="display:table-row">
    <div style="display:table-cell; width:60px; height:60px; background:hotpink;"></div>
    <div style="display:table-cell; width:60px; height:60px; background:yellow;"></div>
  </div>
</div>
""";
        using var bmp = Render(Html);

        AssertPixel(bmp, 30, 30, 255, 105, 180, "the misparented row's first cell did not paint");
        AssertPixel(bmp, 90, 30, 255, 255, 0, "the misparented row's second cell did not paint");
    }

    [Fact]
    public void RowGroupWithoutATable_IsWrappedAndPainted()
    {
        // The shape WPT's table-anonymous-objects family is built from: bare row groups
        // holding bare cells, with no table anywhere above them.
        const string Html = """
<!DOCTYPE html><meta charset="utf-8"><style>body{margin:0}</style>
<div>
  <div style="display:table-row-group">
    <div style="display:table-cell; width:60px; height:60px; background:hotpink;"></div>
  </div>
</div>
""";
        using var bmp = Render(Html);

        AssertPixel(bmp, 30, 30, 255, 105, 180, "the misparented row group's cell did not paint");
    }

    [Fact]
    public void CellWithoutARow_IsWrappedAndPainted()
    {
        // A table-cell needs a table-row parent (rule 1) before the generated row needs a
        // table (rule 2) — both wrappers have to appear for this to paint.
        const string Html = """
<!DOCTYPE html><meta charset="utf-8"><style>body{margin:0}</style>
<div>
  <div style="display:table-cell; width:60px; height:60px; background:hotpink;"></div>
</div>
""";
        using var bmp = Render(Html);

        AssertPixel(bmp, 30, 30, 255, 105, 180, "the misparented cell did not paint");
    }

    [Fact]
    public void ConsecutiveRowGroups_ShareOneAnonymousTable()
    {
        // §17.2.1 wraps a *run* of consecutive proper table children in ONE anonymous table,
        // so two row groups form a single two-row table and their cells share a column — the
        // second group's cell must line up under the first's, not start its own table.
        // The whitespace between them is "treated as if it were display:none", so it must not
        // split the run either.
        const string Html = """
<!DOCTYPE html><meta charset="utf-8"><style>body{margin:0}</style>
<div>
  <div style="display:table-row-group">
    <div style="display:table-cell; width:120px; height:50px; background:hotpink;"></div>
  </div>
  <div style="display:table-row-group">
    <div style="display:table-cell; width:20px; height:50px; background:yellow;"></div>
  </div>
</div>
""";
        using var bmp = Render(Html);

        AssertPixel(bmp, 60, 25, 255, 105, 180, "the first row group's cell did not paint");

        // One shared table means one shared column, so the narrow second cell is stretched to
        // the column width the first cell established. Two separate tables would leave this
        // point on the page background.
        AssertPixel(bmp, 100, 75, 255, 255, 0,
            "the second row group started its own table instead of joining the first's");
    }

    [Fact]
    public void RowInsideARowGroup_IsNotRewrapped()
    {
        // Well-parented content must be left exactly as it is: a row inside a row group inside
        // a table is already correct, and generating anything around it would be a regression.
        const string Html = """
<!DOCTYPE html><meta charset="utf-8"><style>body{margin:0}</style>
<div style="display:table">
  <div style="display:table-row-group">
    <div style="display:table-row">
      <div style="display:table-cell; width:60px; height:50px; background:hotpink;"></div>
    </div>
    <div style="display:table-row">
      <div style="display:table-cell; width:60px; height:50px; background:yellow;"></div>
    </div>
  </div>
</div>
""";
        using var bmp = Render(Html);

        AssertPixel(bmp, 30, 25, 255, 105, 180, "the first row moved");
        AssertPixel(bmp, 30, 75, 255, 255, 0, "the second row moved");
    }

    [Fact]
    public void NonTableContentAroundARun_KeepsItsPlace()
    {
        // The generated table replaces only the run itself; siblings before and after it stay
        // in document order around it, and the anonymous table is block-level so it sits on
        // its own lines between them.
        const string Html = """
<!DOCTYPE html><meta charset="utf-8"><style>body{margin:0}</style>
<div>
  <div style="width:120px; height:40px; background:blue;"></div>
  <div style="display:table-row">
    <div style="display:table-cell; width:120px; height:40px; background:hotpink;"></div>
  </div>
  <div style="width:120px; height:40px; background:yellow;"></div>
</div>
""";
        using var bmp = Render(Html);

        AssertPixel(bmp, 60, 20, 0, 0, 255, "the block before the run moved");
        AssertPixel(bmp, 60, 60, 255, 105, 180, "the wrapped row did not land between its siblings");
        AssertPixel(bmp, 60, 100, 255, 255, 0, "the block after the run moved");
    }
}
