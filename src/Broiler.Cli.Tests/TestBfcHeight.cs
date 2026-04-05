using Broiler.HTML.Image;
using Xunit;

namespace Broiler.Cli.Tests;

public class BfcHeightTests
{
    [Fact]
    public void BfcAbsPos_WithOnlyFloatChild_IncludesFloatInHeight()
    {
        // An absolutely positioned element (BFC) containing only a float.
        // The float should contribute to the parent's auto-height per CSS 2.1 §10.6.7.
        const string html = @"
            <html><body style='margin:0; padding:0'>
                <div style='position:relative; width:400px; height:200px;'>
                    <div style='position:absolute; top:0; left:0;
                                border-left:24px solid black;
                                border-right:24px solid black;
                                border-top:none; border-bottom:none;'>
                        <div style='float:right; width:48px; height:12px;
                                    background:yellow;'></div>
                    </div>
                </div>
            </body></html>";
        using var bitmap = HtmlRender.RenderToImage(html, 500, 200);

        // Count black (border) and yellow (float content) pixels
        int black = 0, yellow = 0;
        for (int y = 0; y < 50; y++)
        for (int x = 0; x < 120; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Red < 10 && px.Green < 10 && px.Blue < 10) black++;
            if (px.Red > 240 && px.Green > 240 && px.Blue < 10) yellow++;
        }

        // The float content should render (yellow pixels present)
        Assert.True(yellow > 50,
            $"Only {yellow} yellow pixels. Float child should render with content.");
        // The side borders should extend to cover the float (black pixels present)  
        Assert.True(black > 50,
            $"Only {black} black pixels. Side borders should extend to float height.");
    }

    [Fact]
    public void BfcAbsPos_EarStructure_RendersCorrectly()
    {
        // Simplified ACID2 ear structure: abs-pos blockquote with side borders
        // containing only a floated address element.
        const string html = @"
            <html><head><style>
                body { margin:0; padding:0; }
                .picture { position: relative; border: 12px solid transparent; margin: 0 0 0 36px; }
                .ears { position: absolute; top: 0; margin: 36px 0 0 60px; padding: 0;
                        border: black 24px; border-style: none solid; }
                .inner { float: right; width: 48px; height: 12px; background: yellow; margin: 0; padding: 0; }
            </style></head>
            <body>
                <div class='picture'>
                    <div class='ears'><div class='inner'></div></div>
                </div>
            </body></html>";
        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);

        int black = 0, yellow = 0;
        for (int y = 0; y < 100; y++)
        for (int x = 0; x < 200; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Red < 10 && px.Green < 10 && px.Blue < 10) black++;
            if (px.Red > 240 && px.Green > 240 && px.Blue < 10) yellow++;
        }

        Assert.True(yellow > 50,
            $"Only {yellow} yellow pixels. Float child should render inside ear.");
        Assert.True(black > 50,
            $"Only {black} black border pixels. Ear borders should render.");
    }
}
