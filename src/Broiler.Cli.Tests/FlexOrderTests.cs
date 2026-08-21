using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSS Flexbox §5.4 / CSS Display §3 <c>order</c>: flex items are laid out and painted in
/// order-modified document order — ascending <c>order</c>, document order within an ordinal group.
/// The property was previously unknown to the engine entirely, so `order` was inert and items always
/// stayed in document order.
/// </summary>
public class FlexOrderTests
{
    private static BBitmap Render(string html) =>
        HtmlRender.RenderToImageWithStyleSet(html, 300, 300);

    private static void AssertPixel(BBitmap bitmap, int x, int y, byte r, byte g, byte b, string what)
    {
        var actual = bitmap.GetPixel(x, y);
        Assert.True(
            actual.R == r && actual.G == g && actual.B == b,
            $"{what} at ({x},{y}) was {actual.R},{actual.G},{actual.B}, expected {r},{g},{b}");
    }

    private const string ColumnHtml = """
<!DOCTYPE html>
<html>
<style>
  body { margin: 0; }
  main { display: flex; flex-direction: column; }
  div { width: 100px; height: 50px; }
  #first { background: red; order: 2; }
  #second { background: blue; }
</style>
<main><div id="first"></div><div id="second"></div></main>
</html>
""";

    private const string RowHtml = """
<!DOCTYPE html>
<html>
<style>
  body { margin: 0; }
  main { display: flex; flex-direction: row; }
  div { width: 100px; height: 50px; }
  #first { background: red; order: 2; }
  #second { background: blue; }
</style>
<main><div id="first"></div><div id="second"></div></main>
</html>
""";

    [Fact(Timeout = 600000)]
    public void Order_Reorders_Column_Flex_Items()
    {
        using var bitmap = Render(ColumnHtml);

        // `first` comes first in the document but carries order:2, so it lays out below `second`.
        AssertPixel(bitmap, 50, 25, 0, 0, 255, "the order:0 item on the first row");
        AssertPixel(bitmap, 50, 75, 255, 0, 0, "the order:2 item on the second row");
    }

    [Fact(Timeout = 600000)]
    public void Order_Reorders_Row_Flex_Items()
    {
        using var bitmap = Render(RowHtml);

        AssertPixel(bitmap, 50, 25, 0, 0, 255, "the order:0 item in the first column");
        AssertPixel(bitmap, 150, 25, 255, 0, 0, "the order:2 item in the second column");
    }

    [Fact(Timeout = 600000)]
    public void Equal_Order_Keeps_Document_Order()
    {
        // The reorder must be stable: an ordinal group holds its document order, and a container
        // where every item shares the initial `order` is untouched.
        const string html = """
<!DOCTYPE html>
<html>
<style>
  body { margin: 0; }
  main { display: flex; flex-direction: column; }
  div { width: 100px; height: 50px; }
  #first { background: red; }
  #second { background: blue; }
</style>
<main><div id="first"></div><div id="second"></div></main>
</html>
""";
        using var bitmap = Render(html);

        AssertPixel(bitmap, 50, 25, 255, 0, 0, "the first item in document order");
        AssertPixel(bitmap, 50, 75, 0, 0, 255, "the second item in document order");
    }

    [Fact(Timeout = 600000)]
    public void Order_Is_Ignored_Outside_A_Flex_Or_Grid_Container()
    {
        // `order` only applies to flex/grid items. In ordinary block flow it must do nothing.
        const string html = """
<!DOCTYPE html>
<html>
<style>
  body { margin: 0; }
  div { width: 100px; height: 50px; }
  #first { background: red; order: 2; }
  #second { background: blue; }
</style>
<main><div id="first"></div><div id="second"></div></main>
</html>
""";
        using var bitmap = Render(html);

        AssertPixel(bitmap, 50, 25, 255, 0, 0, "the block-flow first child, unmoved");
        AssertPixel(bitmap, 50, 75, 0, 0, 255, "the block-flow second child, unmoved");
    }
}
