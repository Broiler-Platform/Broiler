using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

public class Acid3NarrowDiag2Tests
{
    private readonly ITestOutputHelper _output;
    public Acid3NarrowDiag2Tests(ITestOutputHelper output) { _output = output; }

    private string CheckBg(string desc, string css, int x=100, int y=100)
    {
        var html = $@"<!DOCTYPE html><html><head><style>{css}</style></head>
<body><p>Test</p></body></html>";
        using var bitmap = HtmlRender.RenderToImage(html, 800, 600);
        var px = bitmap.GetPixel(x, y);
        var result = $"{desc}: ({x},{y}) R={px.Red} G={px.Green} B={px.Blue}";
        _output.WriteLine(result);
        return result;
    }

    [Fact]
    public void Narrow_Step_By_Step()
    {
        var dataUri = "data:image/gif;base64,iVBORw0KGgoAAAANSUhEUgAAABQAAAAUCAIAAAAC64paAAAABGdBTUEAAK%2FINwWK6QAAAAlwSFlzAAAASAAAAEgARslrPgAAABtJREFUOMtj%2FM9APmCiQO%2Bo5lHNo5pHNVNBMwAinAEnIWw89gAAACJ6VFh0U29mdHdhcmUAAHjac0zJT0pV8MxNTE8NSk1MqQQAL5wF1K4MqU0AAAAASUVORK5CYII%3D";
        
        // Base that works: 
        var baseCss = $@"* {{ margin: 0; border: 1px blue; padding: 0; border-spacing: 0; font: inherit; line-height: 1.2; color: inherit; background: transparent; }}
html {{ background: silver; }}
body {{ background: url({dataUri}) no-repeat 99.8392283% 1px white; }}";
        
        // Add html font 
        CheckBg("A-add-html-font", baseCss + " html { font: 20px Arial, sans-serif; }", 200, 200);
        
        // Add html width
        CheckBg("B-add-html-width", baseCss + " html { font: 20px Arial, sans-serif; width: 32em; }", 200, 200);
        
        // Add html margin
        CheckBg("C-add-html-margin", baseCss + " html { font: 20px Arial, sans-serif; width: 32em; margin: 1em; }", 200, 200);
        
        // Add html border
        CheckBg("D-add-html-border", baseCss + @" html { font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; } html { border-width: 0 0.2em 0.2em 0; }", 200, 200);

        // Add body border
        CheckBg("E-add-body-border", baseCss + @" html { font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; } html { border-width: 0 0.2em 0.2em 0; } body { border: solid 1px black; }", 200, 200);

        // Add body padding
        CheckBg("F-add-body-padding", baseCss + @" html { font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; } html { border-width: 0 0.2em 0.2em 0; } body { padding: 2em 2em 0; border: solid 1px black; }", 200, 200);
        
        // Add body margin  
        CheckBg("G-add-body-margin", baseCss + @" html { font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; } html { border-width: 0 0.2em 0.2em 0; } body { padding: 2em 2em 0; border: solid 1px black; margin: -0.2em 0 0 -0.2em; }", 200, 200);
        
        // Full - the known-broken case 
        CheckBg("H-full-broken", $@"* {{ margin: 0; border: 1px blue; padding: 0; border-spacing: 0; font: inherit; line-height: 1.2; color: inherit; background: transparent; }}
html {{ font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; }}
html {{ background: silver; color: black; border-width: 0 0.2em 0.2em 0; }}
body {{ padding: 2em 2em 0; background: url({dataUri}) no-repeat 99.8392283% 1px white; border: solid 1px black; margin: -0.2em 0 0 -0.2em; }}", 200, 200);

        // Check multiple points to find body area
        _output.WriteLine("=== Sampling full broken case at multiple points ===");
        var fullCss = $@"* {{ margin: 0; border: 1px blue; padding: 0; border-spacing: 0; font: inherit; line-height: 1.2; color: inherit; background: transparent; }}
html {{ font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; }}
html {{ background: silver; color: black; border-width: 0 0.2em 0.2em 0; }}
body {{ padding: 2em 2em 0; background: url({dataUri}) no-repeat 99.8392283% 1px white; border: solid 1px black; margin: -0.2em 0 0 -0.2em; }}";
        
        var html = $@"<!DOCTYPE html><html><head><style>{fullCss}</style></head>
<body><p>Test</p></body></html>";
        using var bitmap = HtmlRender.RenderToImage(html, 800, 600);
        for (int yy = 0; yy < 600; yy += 50) {
            for (int xx = 0; xx < 700; xx += 100) {
                var px = bitmap.GetPixel(xx, yy);
                if (px.Red != 255 || px.Green != 255 || px.Blue != 255)
                    _output.WriteLine($"  ({xx},{yy}): R={px.Red} G={px.Green} B={px.Blue}");
            }
        }
    }
}
