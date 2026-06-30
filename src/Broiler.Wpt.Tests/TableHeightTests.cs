using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression test for CSS2.1 §17.5.3 table-height distribution (WPT issue
/// #1143). A specified table <c>height</c> greater than the rows' natural
/// content height must be distributed over the rows; Broiler previously sized
/// the table purely from content, so a `height:2in` table (the entire CSS2
/// tables suite) rendered collapsed.
/// </summary>
public class TableHeightTests : IDisposable
{
    private readonly string _tempDir;

    public TableHeightTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-tableh-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private int RenderedTableHeight(string html)
    {
        var file = Path.Combine(_tempDir, "t-" + Guid.NewGuid().ToString("N")[..6] + ".html");
        File.WriteAllText(file, html);
        var runner = new WptTestRunner(300, 400);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, _tempDir);
        int top = -1, bottom = -1;
        for (int y = 0; y < 400; y++)
            for (int x = 0; x < 250; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.R < 80 && p.G < 80 && p.B < 80) // black cell borders
                {
                    if (top < 0) top = y;
                    bottom = y;
                    break;
                }
            }
        return (top < 0) ? 0 : bottom - top;
    }

    [Fact]
    public void SpecifiedTableHeight_GreaterThanContent_IsDistributedOverRows()
    {
        // Two rows of short cells: natural height is ~40px, but the table asks
        // for 200px — the rows must grow to fill it.
        var html = @"<!DOCTYPE html><html><head><style>
table { border-collapse: collapse; height: 200px; }
td { border: 2px solid black; width: 50px; }
</style></head><body>
<table><tr><td>a</td></tr><tr><td>b</td></tr></table></body></html>";
        int h = RenderedTableHeight(html);
        Assert.True(h > 150, $"table should fill its specified 200px height, got ~{h}px");
    }
}
