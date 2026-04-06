using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;

namespace Broiler.Cli.Tests;

public class DiagFormRenderTests
{
    [Fact]
    public void RenderFormControls_DetailedDiag()
    {
        var html = @"<!doctype html>
<html>
<head>
<style>
body { margin: 20px; font-family: Arial, sans-serif; }
</style>
</head>
<body>
<h2>Form Controls Test</h2>
<input type=""text"" value=""Hello World"" />
<input type=""submit"" value=""Search"" />
<button>Click Me</button>
<br/><br/>
<input type=""text"" />
<input type=""submit"" value=""Go"" />
</body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 800, 400);
        
        using (var fs = new System.IO.FileStream("/tmp/form_render.png", System.IO.FileMode.Create))
        {
            bitmap.Encode(fs, SKEncodedImageFormat.Png, 100);
        }

        var output = new System.Text.StringBuilder();
        output.AppendLine($"Image size: {bitmap.Width}x{bitmap.Height}");
        
        // For each non-white row in the input area, show exact x-ranges
        for (int y = 40; y <= 140; y++)
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            int count = 0;
            for (int x = 0; x < bitmap.Width; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red < 245 || px.Green < 245 || px.Blue < 245)
                {
                    count++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                }
            }
            if (count > 0)
                output.AppendLine($"  y={y}: {count} px, x=[{minX}..{maxX}] (width {maxX-minX+1})");
        }
        
        Assert.Fail(output.ToString());
    }
    
    [Fact]
    public void RenderSimpleInputOnly_Diag()
    {
        // Just a single text input with explicit width - most basic test
        var html = @"<!doctype html>
<html><body>
<input type=""text"" value=""Hello"" style=""width:200px; height:30px; border:2px solid red;"" />
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);
        
        using (var fs = new System.IO.FileStream("/tmp/simple_input.png", System.IO.FileMode.Create))
        {
            bitmap.Encode(fs, SKEncodedImageFormat.Png, 100);
        }

        var output = new System.Text.StringBuilder();
        output.AppendLine($"Simple input - Image size: {bitmap.Width}x{bitmap.Height}");
        
        // Check for any red pixels (border)
        int redPixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50)
                    redPixels++;
            }
        output.AppendLine($"Red border pixels: {redPixels}");
        
        for (int y = 0; y <= 60; y++)
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            int count = 0;
            for (int x = 0; x < bitmap.Width; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red < 245 || px.Green < 245 || px.Blue < 245)
                {
                    count++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                }
            }
            if (count > 0)
                output.AppendLine($"  y={y}: {count} px, x=[{minX}..{maxX}] (width {maxX-minX+1})");
        }
        
        Assert.Fail(output.ToString());
    }

    [Fact]
    public void RenderGoogleLikeSearch_Diag()
    {
        var html = @"<!doctype html>
<html>
<head>
<style>
body { margin:0; font-family:Arial,sans-serif; }
.search-box { margin:20px auto; width:580px; }
.search-box input[type=""text""] {
  width:100%; padding:12px 16px; font-size:16px;
  border:1px solid #dfe1e5; border-radius:24px; outline:none;
}
.buttons { margin-top:20px; text-align:center; }
.buttons input[type=""submit""] {
  background:#f8f9fa; border:1px solid #f8f9fa; border-radius:4px;
  color:#3c4043; font-size:14px; margin:0 4px; padding:10px 16px;
}
</style>
</head>
<body>
<div style=""text-align:center; margin-top:50px;"">
  <div class=""search-box"">
    <input type=""text"" name=""q"" title=""Search"">
  </div>
  <div class=""buttons"">
    <input type=""submit"" value=""Google Search"">
    <input type=""submit"" value=""I'm Feeling Lucky"">
  </div>
</div>
</body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 800, 300);
        
        using (var fs = new System.IO.FileStream("/tmp/google_search.png", System.IO.FileMode.Create))
        {
            bitmap.Encode(fs, SKEncodedImageFormat.Png, 100);
        }

        var output = new System.Text.StringBuilder();
        output.AppendLine($"Google Search - Image size: {bitmap.Width}x{bitmap.Height}");
        
        // Check bands
        for (int band = 0; band < 300; band += 5)
        {
            int count = 0;
            int minX = int.MaxValue, maxX = int.MinValue;
            for (int y = band; y < Math.Min(band + 5, bitmap.Height); y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var px = bitmap.GetPixel(x, y);
                    if (px.Red < 245 || px.Green < 245 || px.Blue < 245)
                    {
                        count++;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                    }
                }
            if (count > 0)
                output.AppendLine($"  y={band}-{band+4}: {count} px, x=[{minX}..{maxX}]");
        }
        
        Assert.Fail(output.ToString());
    }
}
