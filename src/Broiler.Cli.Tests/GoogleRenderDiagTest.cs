using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class GoogleRenderDiagTest(ITestOutputHelper output)
{
    /// <summary>
    /// Simulate a simplified version of Google.de search page to identify
    /// specific misalignment issues visible in the screenshot:
    /// 1. Three full-width gray bars (buttons should be content-sized)
    /// 2. Footer links running together without spacing
    /// 3. Search input box oversized
    /// </summary>
    [Fact]
    public void GoogleDe_LayoutDiag()
    {
        // Simplified Google.de HTML structure matching what Broiler would receive
        var html = @"<html><body style='margin:0; font-family:arial,sans-serif; font-size:14px'>
<div style='text-align:right; padding:10px'>
    <a href='#'>Gmail</a> <a href='#'>Bilder</a>
    <div><a href='#' style='color:blue'>Anmelden</a></div>
</div>
<center>
<form style='width:600px'>
    <input name='q' type='text' style='width:500px; height:44px; border:1px solid #dfe1e5; border-radius:24px; padding:0 20px; font-size:16px'><br>
    <div style='display:flex; justify-content:center; margin-top:18px'>
        <input type='submit' name='btnK' value='Google Suche' style='background:#f8f9fa; border:1px solid #f8f9fa; border-radius:4px; color:#3c4043; font-size:14px; margin:0 4px; padding:0 16px; height:36px; min-width:54px; cursor:pointer'>
        <input type='submit' name='btnI' value='Auf gut Glueck!' style='background:#f8f9fa; border:1px solid #f8f9fa; border-radius:4px; color:#3c4043; font-size:14px; margin:0 4px; padding:0 16px; height:36px; min-width:54px; cursor:pointer'>
    </div>
</form>
</center>
<div style='position:absolute; bottom:0; width:100%; background:#f2f2f2; padding:15px 0'>
    <div style='text-align:center; font-size:14px; color:#70757a'>
        <span>&copy;2026</span>
        <a href='#'>Datenschutzerkl&auml;rung</a>
        <a href='#'>Nutzungsbedingungen</a>
    </div>
    <div style='text-align:center; font-size:14px; color:#70757a; margin-top:10px'>
        <a href='#'>Werbeprogramme</a>
        <a href='#'>Unternehmensangebote</a>
        <a href='#'>&Uuml;ber Google</a>
        <a href='#'>Google.de</a>
    </div>
</div>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 600);
        
        // Save to PNG for visual inspection
        using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        var path = "/tmp/google_render.png";
        using (var fs = System.IO.File.OpenWrite(path))
            data.SaveTo(fs);
        output.WriteLine($"Saved render to {path}");
        
        // === ISSUE 1: Check that buttons are NOT full-width gray bars ===
        // Look for rows in button area (y=60-120) that span most of the width
        bool hasFullWidthBar = false;
        int buttonAreaStart = -1, buttonAreaEnd = -1;
        for (int y = 60; y < 200; y++)
        {
            int grayCount = 0;
            int leftMost = -1, rightMost = -1;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                // #f8f9fa or similar light gray background
                if (px.Red >= 230 && px.Red <= 255 && px.Green >= 230 && px.Green <= 255 && px.Blue >= 230 && px.Blue <= 255
                    && (px.Red < 254 || px.Green < 254 || px.Blue < 254))
                {
                    grayCount++;
                    if (leftMost < 0) leftMost = x;
                    rightMost = x;
                }
            }
            if (grayCount > 600) // More than 600px wide = full-width bar
            {
                hasFullWidthBar = true;
                output.WriteLine($"ISSUE: Full-width gray bar at y={y}: gray={grayCount}px, left={leftMost}, right={rightMost}");
                if (buttonAreaStart < 0) buttonAreaStart = y;
                buttonAreaEnd = y;
            }
        }
        
        // === ISSUE 2: Check button text visibility ===
        int buttonDarkPixels = 0;
        for (int y = 60; y < 200; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 100 && px.Green < 100 && px.Blue < 100)
                    buttonDarkPixels++;
            }
        }
        output.WriteLine($"Button area (y=60-200) dark text pixels: {buttonDarkPixels}");

        // === ISSUE 3: Check spacing between footer links ===
        // Look at last 100 rows for text
        for (int y = bmp.Height - 100; y < bmp.Height; y++)
        {
            int darkInRow = 0;
            int leftMost = -1, rightMost = -1;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 150 && px.Green < 150 && px.Blue < 150)
                {
                    darkInRow++;
                    if (leftMost < 0) leftMost = x;
                    rightMost = x;
                }
            }
            if (darkInRow > 10)
                output.WriteLine($"Footer text at y={y}: dark={darkInRow}, left={leftMost}, right={rightMost}");
        }

        // === ISSUE 4: Check button widths (should be content-sized) ===
        // Look for individual button extents in button area
        output.WriteLine("--- Scanning for button extents ---");
        for (int y = 60; y < 200; y += 5)
        {
            int leftMost = -1, rightMost = -1;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 252 || px.Green < 252 || px.Blue < 252)
                {
                    if (leftMost < 0) leftMost = x;
                    rightMost = x;
                }
            }
            if (leftMost >= 0)
                output.WriteLine($"  y={y}: content left={leftMost}, right={rightMost}, width={rightMost - leftMost + 1}");
        }
        
        Assert.False(hasFullWidthBar, "Buttons should NOT render as full-width gray bars");
        Assert.True(buttonDarkPixels > 50, $"Buttons should have visible text (got {buttonDarkPixels} dark pixels)");
    }
    
    /// <summary>
    /// Test that whitespace between anchor elements produces visual spacing.
    /// Footer links should not run together.
    /// </summary>
    [Fact]
    public void FooterLinks_HaveSpacing()
    {
        var html = @"<html><body style='margin:0; font-size:14px'>
<div style='text-align:center'>
    <a href='#'>Datenschutz</a>
    <a href='#'>Nutzungsbedingungen</a>
    <a href='#'>Werbeprogramme</a>
</div>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 600, 40);
        
        // Check that there are gaps (white columns) between the links
        int gapCount = 0;
        bool inText = false;
        for (int x = 0; x < bmp.Width; x++)
        {
            bool columnHasDark = false;
            for (int y = 0; y < bmp.Height; y++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 200 && px.Green < 200 && px.Blue < 200)
                {
                    columnHasDark = true;
                    break;
                }
            }
            if (columnHasDark && !inText)
            {
                inText = true;
            }
            else if (!columnHasDark && inText)
            {
                inText = false;
                gapCount++;
            }
        }
        
        output.WriteLine($"Found {gapCount} gaps between text segments");
        // With 3 links, we should have at least 2 gaps between them
        // (plus gaps between words within links)
        Assert.True(gapCount >= 2, $"Should have gaps between links (found {gapCount})");
    }
    
    /// <summary>
    /// Test that the search text input is not oversized.
    /// </summary>
    [Fact]  
    public void SearchInput_RespectsCSSWidth()
    {
        var html = @"<html><body style='margin:0'>
<center>
    <input type='text' style='width:500px; height:44px; border:1px solid #dfe1e5'>
</center>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 80);
        
        // Find the horizontal extent of the input box
        int inputLeft = bmp.Width, inputRight = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                {
                    if (x < inputLeft) inputLeft = x;
                    if (x > inputRight) inputRight = x;
                }
            }
        }
        int inputWidth = inputRight - inputLeft + 1;
        output.WriteLine($"Input width: {inputWidth}px (left={inputLeft}, right={inputRight})");
        
        // Should be approximately 500px + 2px border = 502px, not 800px
        Assert.True(inputWidth < 600, $"Input should respect width:500px (actual={inputWidth})");
    }
}
