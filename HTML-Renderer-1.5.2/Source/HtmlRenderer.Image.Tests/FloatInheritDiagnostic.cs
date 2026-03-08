// Diagnostic test for float:inherit
// Place in test project and run

using System;
using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

public class FloatInheritDiagnostic
{
    [Fact]
    public void FloatInherit_ResolvesFromParent()
    {
        // Minimal reproduction of the Acid2 smile structure
        string html = @"
        <html><head><style>
            .parent { float: right; width: 100px; height: 50px; background: red; }
            .parent .child { float: inherit; width: 50px; height: 25px; background: blue; }
        </style></head><body>
            <div class='parent'><div class='child'>X</div></div>
        </body></html>";

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(400, 400);
        container.SetHtml(html);

        using var bmp = new SKBitmap(400, 400, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        container.PerformLayout(canvas, new RectangleF(0, 0, 400, 400));
        container.PerformPaint(canvas, new RectangleF(0, 0, 400, 400));

        // If float:inherit works correctly, the child should float right
        // Check that the blue child box is positioned to the right of the parent's content area
        // A floated right child should be at the right edge of its containing block
        
        // Check for blue pixels (child should be visible)
        int blueCount = 0;
        int rightmostBlue = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Blue > 200 && px.Red < 50 && px.Green < 50)
                {
                    blueCount++;
                    rightmostBlue = Math.Max(rightmostBlue, x);
                }
            }
        }

        Console.WriteLine($"Blue pixels: {blueCount}, Rightmost blue X: {rightmostBlue}");
        
        // If float:inherit resolves to float:right, the child should be floating right
        // The rightmost blue pixel should be near the right edge of the parent
        Assert.True(blueCount > 0, "No blue pixels found - child may not be rendering");
    }

    [Fact]
    public void SmileStructure_NestedFloats_Layout()
    {
        // More complete reproduction of the Acid2 smile structure
        string html = @"
        <html><head><style>
            body { font-size: 12px; }
            .smile { margin: 5em 3em; clear: both; }
            .smile div { margin-top: 0.25em; background: black; width: 12em; height: 2em; position: relative; bottom: -1em; }
            .smile div div { position: absolute; top: 0; right: 1em; width: auto; height: 0; margin: 0; border: yellow solid 1em; }
            .smile div div span { display: inline; margin: -1em 0 0 0; border: solid 1em transparent; border-style: none solid; float: right; background: black; height: 1em; }
            .smile div div span em { float: inherit; border-top: solid yellow 1em; border-bottom: solid black 1em; }
            .smile div div span em strong { width: 6em; display: block; margin-bottom: -1em; }
        </style></head><body>
            <div class='smile'><div><div><span><em><strong></strong></em></span></div></div></div>
        </body></html>";

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(400, 400);
        container.SetHtml(html);

        using var bmp = new SKBitmap(400, 400, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        container.PerformLayout(canvas, new RectangleF(0, 0, 400, 400));
        container.PerformPaint(canvas, new RectangleF(0, 0, 400, 400));
        
        // Count pixels by color
        int blackCount = 0, yellowCount = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 20 && px.Green < 20 && px.Blue < 20) blackCount++;
                if (px.Red > 230 && px.Green > 230 && px.Blue < 50) yellowCount++;
            }
        }

        Console.WriteLine($"Black pixels: {blackCount}, Yellow pixels: {yellowCount}");
        
        // Save for visual inspection
        using var stream = File.OpenWrite("/tmp/smile_test.png");
        bmp.Encode(stream, SKEncodedImageFormat.Png, 100);
        Console.WriteLine("Saved to /tmp/smile_test.png");
    }
}
