using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

public class Acid3FullDiagTests
{
    private readonly ITestOutputHelper _output;
    public Acid3FullDiagTests(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void Diag_Render_ProcessedHtml_Directly()
    {
        // Read the already-processed acid3 HTML
        var html = File.ReadAllText("/tmp/acid3-processed.html");
        
        using var bitmap = HtmlRender.RenderToImage(html, 800, 600);
        
        // Sample pixels
        _output.WriteLine($"Image size: {bitmap.Width}x{bitmap.Height}");
        
        // Check body area for background color  
        for (int y = 60; y < 300; y += 30) {
            for (int x = 60; x < 400; x += 60) {
                var px = bitmap.GetPixel(x, y);
                _output.WriteLine($"Pixel ({x},{y}): R={px.Red} G={px.Green} B={px.Blue}");
            }
        }
        
        // Check if center of body area is white
        var bodyPx = bitmap.GetPixel(300, 150);
        _output.WriteLine($"\nBody area (300,150): R={bodyPx.Red} G={bodyPx.Green} B={bodyPx.Blue}");
    }
    
    [Fact]
    public void Diag_Render_MinimalAcid3Css()
    {
        // Minimal reproduction with actual acid3 CSS (just the relevant rules)
        var html = @"<!DOCTYPE html><html><head><style type='text/css'>
* { margin: 0; border: 1px blue; padding: 0; border-spacing: 0; font: inherit; line-height: 1.2; color: inherit; background: transparent; }
html { font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; }
html { background: silver; color: black; border-width: 0 0.2em 0.2em 0; }
body { padding: 2em 2em 0; background: url(data:image/gif;base64,iVBORw0KGgoAAAANSUhEUgAAABQAAAAUCAIAAAAC64paAAAABGdBTUEAAK%2FINwWK6QAAAAlwSFlzAAAASAAAAEgARslrPgAAABtJREFUOMtj%2FM9APmCiQO%2Bo5lHNo5pHNVNBMwAinAEnIWw89gAAACJ6VFh0U29mdHdhcmUAAHjac0zJT0pV8MxNTE8NSk1MqQQAL5wF1K4MqU0AAAAASUVORK5CYII%3D) no-repeat 99.8392283% 1px white; border: solid 1px black; margin: -0.2em 0 0 -0.2em; }
h1:first-child { cursor: help; font-size: 5em; font-weight: bolder; margin-bottom: -0.4em; }
.buckets { font: 0/0 Arial, sans-serif; }
.buckets { padding: 0 0 150px 3px; }
:first-child + * .buckets p { display: inline-block; vertical-align: 2em; border: 2em dotted red; padding: 1.0em 0 1.0em 2em; }
* + * > * > p { margin: 0; border: 1px solid ! important; }
#bucket1 { font-size: 20px; margin-left: 0.2em; padding-left: 1.3em; padding-right: 1.3em; }
#bucket2 { font-size: 24px; margin-left: 0.375em; padding-left: 30px; padding-right: 32px; margin-right: 2px; }
#bucket3 { font-size: 28px; margin-left: 8.9999px; padding-left: 17px; padding-right: 55px; margin-right: 12px; }
#bucket4 { font-size: 32px; margin-left: 0; padding-left: 84px; padding-right: 0; }
#bucket5 { font-size: 36px; margin-left: 13px; padding-left: 0; padding-right: 94px; margin-right: 25px; }
#bucket6 { font-size: 40px; margin-left: -10px; padding-left: 104px; padding-right: -10px; }
#bucket1.zPPPPPPPPPPPPPPPP { background: red; }
#bucket2.zPPPPPPPPPPPPPPPP { background: orange; }
#bucket3.zPPPPPPPPPPPPPPPP { background: yellow; }
#bucket4.zPPPPPPPPPPPPPPPP { background: lime; }
#bucket5.zPPPPPPPPPPPPPPPP { background: blue; }
#bucket6.zPPPPPPPPPPPPPPPP { background: purple; }
</style></head>
<body>
<h1>Acid3</h1>
<div class='buckets'><p id='bucket1' class='zPPPPPPPPPPPPPPPP'></p><p id='bucket2' class='zPPPPPPPPPPPPPPPP'></p><p id='bucket3' class='zPPPPPPPPPPPPPPPP'></p><p id='bucket4' class='zPPPPPPPPPPPPPPPP'></p><p id='bucket5' class='zPPPPPPPPPPPPPPPP'></p><p id='bucket6' class='zPPPPPPPPPPPPPPPP'></p></div>
<p id='result'><span id='score'>100</span><span id='slash'>/</span><span>100</span></p>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 800, 600);
        bitmap.Encode(new FileStream("/tmp/acid3-minimal.png", FileMode.Create), SkiaSharp.SKEncodedImageFormat.Png, 100);
        
        // Check body background  
        var bodyPx = bitmap.GetPixel(300, 300);
        _output.WriteLine($"Body area (300,300): R={bodyPx.Red} G={bodyPx.Green} B={bodyPx.Blue}");
        
        // Check a few areas
        for (int y = 0; y < 600; y += 50) {
            var px = bitmap.GetPixel(200, y);
            _output.WriteLine($"At (200,{y}): R={px.Red} G={px.Green} B={px.Blue}");
        }
    }
}
