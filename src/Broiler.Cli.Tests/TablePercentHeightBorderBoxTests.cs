using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

// WPT css/css-sizing/table-child-percentage-height-with-border-box: body is display:table
// (border-box, 100%x100%), .content is a table-row (100% height, red), .wrapper is border-box
// height:100% + padding-top:100px, green. Per the reference the green wrapper resolves to the
// full row height and covers the red content — the viewport is entirely green.
public class TablePercentHeightBorderBoxTests
{
    private const string Html = """
<!DOCTYPE html>
<html>
<head><style>
  html { height: 100%; width: 100%; }
  body { box-sizing: border-box; display: table; margin: 0 auto; width: 100%; height: 100%; }
  .content { box-sizing: border-box; display: table-row; width: 100%; height: 100%; background-color: red; }
  .wrapper { box-sizing: border-box; width: 100%; height: 100%; background-color: green; padding-top: 100px; }
</style></head>
<body>
  <div class="content"><div class="wrapper">wrapped content</div></div>
</body>
</html>
""";

    [Fact(Timeout = 600000)]
    public void TableRowChild_PercentHeight_BorderBox_FillsRowGreen()
    {
        using var bmp = HtmlRender.RenderToImageWithStyleSet(Html, 300, 300);
        // Below the 100px padding and near the bottom: the green wrapper must fill the whole
        // table height, with no red (uncovered row) showing through.
        var mid = bmp.GetPixel(150, 180);
        var bottom = bmp.GetPixel(150, 290);
        Assert.True((mid.R, mid.G, mid.B) == (0, 128, 0), $"mid expected green, was {mid.R},{mid.G},{mid.B}");
        Assert.True((bottom.R, bottom.G, bottom.B) == (0, 128, 0), $"bottom expected green, was {bottom.R},{bottom.G},{bottom.B}");
    }
}
