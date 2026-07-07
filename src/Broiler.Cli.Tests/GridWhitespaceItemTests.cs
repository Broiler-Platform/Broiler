using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression guard for CSS Grid §4: a contiguous run of collapsible white space
/// that is not inside an element generates no grid item. Broiler's box tree keeps
/// the inter-item white space between block-level grid items as anonymous
/// whitespace-only boxes; the grid item collection previously turned each into a
/// phantom item, auto-placing it into its own track and inflating the grid with
/// spurious rows/columns (the grid-lanes subgrid reftests, e.g.
/// row-subgrid-grid-gap-012, gained extra rows and jumped from ~46% to ~96% pixel
/// match once these were dropped).
///
/// A three-row grid whose items carry newlines/indentation between them must size
/// to exactly three rows — identical to the same markup with the white space
/// removed.
/// </summary>
public sealed class GridWhitespaceItemTests
{
    private static double GridHeight(string bodyInner)
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + "body{margin:0;font:20px/1 monospace}"
            + ".g{display:grid;grid-template-columns:100px;grid-auto-rows:40px;position:relative}"
            + ".g span{background:grey}"
            + "</style></head><body>"
            + "<div class=\"g\" id=\"g\" data-offset-x=\"0\" data-offset-y=\"0\" data-expected-height=\"0\">"
            + bodyInner
            + "</div></body></html>";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///ws.html");
        var by = bridge.EvaluateCheckLayoutAssertions()
            .GroupBy(a => a.Element)
            .ToDictionary(g => g.Key, g => g.ToDictionary(a => a.Property, a => a.Actual));
        return by.TryGetValue("div#g", out var m) && m.TryGetValue("height", out var h) ? h : double.NaN;
    }

    [Fact]
    public void WhitespaceBetweenGridItems_DoesNotAddPhantomTracks()
    {
        // Newlines + indentation between the three items (the natural authoring
        // style, and exactly what the WPT subgrid tests use).
        string spaced =
            "\n      <span style=\"grid-row:1\">a</span>\n"
            + "      <span style=\"grid-row:2\">b</span>\n"
            + "      <span style=\"grid-row:3\">c</span>\n    ";
        // The same three items with no inter-item white space.
        string tight =
            "<span style=\"grid-row:1\">a</span>"
            + "<span style=\"grid-row:2\">b</span>"
            + "<span style=\"grid-row:3\">c</span>";

        double spacedH = GridHeight(spaced);
        double tightH = GridHeight(tight);

        // Three 40px auto rows → 120px, regardless of the inter-item white space.
        Assert.Equal(120, tightH, 1);
        Assert.Equal(tightH, spacedH, 1);
    }
}
