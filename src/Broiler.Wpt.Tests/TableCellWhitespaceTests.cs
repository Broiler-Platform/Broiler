using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression test for CSS Text 3 §4.1.1 leading/trailing white-space removal
/// inside table cells (WPT issue #1147,
/// <c>css/CSS2/tables/table-anonymous-objects-*</c>).
///
/// A table cell establishes its own formatting context, so collapsible
/// white space at the start of its first line and the end of its last line is
/// removed. Broiler models a collapsed space as word-spacing carried on the
/// neighbouring word (<c>HasSpaceBefore</c>/<c>HasSpaceAfter</c>); the
/// shrink-to-fit width sum in <c>CssBoxHelper.GetMinMaxSumWords</c> counted that
/// edge spacing, so a whitespace-padded cell (<c>&lt;td&gt; Xy &lt;/td&gt;</c>)
/// computed a wider intrinsic width than the tight <c>&lt;td&gt;Xy&lt;/td&gt;</c>,
/// even though the paint path already drops the edge spaces. Adjacent cells then
/// failed to abut, which is exactly what the anonymous-table reftests detect by
/// overlaying a <c>display:table</c> construct on a real <c>&lt;table&gt;</c>.
/// The fix subtracts the formatting context's edge white-space from the
/// preferred width in <c>CssBox.GetMinMaxWidth</c>.
/// </summary>
public class TableCellWhitespaceTests : IDisposable
{
    private readonly string _tempDir;

    public TableCellWhitespaceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-cellws-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // Rightmost black (text) pixel across the whole bitmap — the content extent.
    private int ContentRight(string html)
    {
        var file = Path.Combine(_tempDir, "t-" + Guid.NewGuid().ToString("N")[..6] + ".html");
        File.WriteAllText(file, html);
        var runner = new WptTestRunner(400, 120);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, _tempDir);
        int right = -1;
        for (int y = 0; y < 120; y++)
            for (int x = 0; x < 400; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.R < 90 && p.G < 90 && p.B < 90)
                    if (x > right) right = x;
            }
        return right;
    }

    private const string Style =
        "<!DOCTYPE html><html><head><style>body{margin:0;font:20px sans-serif}"
        + "table{border-collapse:collapse;border:none}td{border:none;padding:0}"
        + "</style></head><body>";

    [Fact]
    public void WhitespacePaddedCells_AbutLikeTightCells()
    {
        // Tight cells: <td>Xy</td><td>Xy</td>.
        int tight = ContentRight(Style
            + "<table cellspacing=\"0\" cellpadding=\"0\"><tr><td>Xy</td><td>Xy</td></tr></table></body></html>");

        // Same cells, but each cell's content is wrapped in collapsible
        // white space (newlines + indentation), as in the WPT reftests.
        int padded = ContentRight(Style
            + "<table cellspacing=\"0\" cellpadding=\"0\"><tr><td>\n  Xy\n </td><td>\n  Xy\n </td></tr></table></body></html>");

        Assert.True(tight > 0, "tight table did not paint any text.");
        Assert.True(padded > 0, "padded table did not paint any text.");
        // Leading/trailing white space inside each cell must not widen it, so
        // the second cell starts at the same place and the content ends at the
        // same x in both tables (allow 1px for anti-aliasing).
        Assert.True(System.Math.Abs(padded - tight) <= 1,
            $"whitespace-padded cells did not abut like tight cells: tight right={tight}, padded right={padded}.");
    }

    [Fact]
    public void DisplayTableCells_MatchRealTable()
    {
        // A real <table> with tight cells …
        int real = ContentRight(Style
            + "<table cellspacing=\"0\" cellpadding=\"0\"><tr><td>Xy</td><td>Xy</td></tr></table></body></html>");

        // … and the equivalent display:table construct with whitespace-padded
        // table-cell spans (the anonymous-table reftest shape) must produce the
        // same content extent.
        int css = ContentRight(Style
            + "<span style=\"display:table\">"
            + "<span style=\"display:table-cell\">\n  Xy\n </span>"
            + "<span style=\"display:table-cell\">\n  Xy\n </span>"
            + "</span></body></html>");

        Assert.True(real > 0 && css > 0, "a construct did not paint any text.");
        Assert.True(System.Math.Abs(css - real) <= 1,
            $"display:table cells did not match the real table: real right={real}, css right={css}.");
    }
}
