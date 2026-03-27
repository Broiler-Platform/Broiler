using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

public class Acid3NarrowDiagTests
{
    private readonly ITestOutputHelper _output;
    public Acid3NarrowDiagTests(ITestOutputHelper output) { _output = output; }

    private string CheckBg(string desc, string css)
    {
        var html = $@"<!DOCTYPE html><html><head><style>{css}</style></head>
<body><p>Test</p></body></html>";
        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        var px = bitmap.GetPixel(50, 50);
        var result = $"{desc}: R={px.Red} G={px.Green} B={px.Blue}";
        _output.WriteLine(result);
        return result;
    }

    [Fact]
    public void Narrow_Down_Red_Background()
    {
        var dataUri = "data:image/gif;base64,iVBORw0KGgoAAAANSUhEUgAAABQAAAAUCAIAAAAC64paAAAABGdBTUEAAK%2FINwWK6QAAAAlwSFlzAAAASAAAAEgARslrPgAAABtJREFUOMtj%2FM9APmCiQO%2Bo5lHNo5pHNVNBMwAinAEnIWw89gAAACJ6VFh0U29mdHdhcmUAAHjac0zJT0pV8MxNTE8NSk1MqQQAL5wF1K4MqU0AAAAASUVORK5CYII%3D";
        
        // Test 1: Just body background shorthand with this exact data URI
        CheckBg("1-body-only", 
            $"body {{ background: url({dataUri}) no-repeat 99.8392283% 1px white; }}");

        // Test 2: Add * { background: transparent }
        CheckBg("2-star-bg", 
            $"* {{ background: transparent; }} body {{ background: url({dataUri}) no-repeat 99.8392283% 1px white; }}");

        // Test 3: Add the full * rule
        CheckBg("3-full-star", 
            $"* {{ margin: 0; border: 1px blue; padding: 0; border-spacing: 0; font: inherit; line-height: 1.2; color: inherit; background: transparent; }} body {{ background: url({dataUri}) no-repeat 99.8392283% 1px white; }}");

        // Test 4: Add html background
        CheckBg("4-html-bg", 
            $"* {{ margin: 0; border: 1px blue; padding: 0; border-spacing: 0; font: inherit; line-height: 1.2; color: inherit; background: transparent; }} html {{ background: silver; }} body {{ background: url({dataUri}) no-repeat 99.8392283% 1px white; }}");

        // Test 5: Full acid3 header CSS
        CheckBg("5-full-header", 
            $@"* {{ margin: 0; border: 1px blue; padding: 0; border-spacing: 0; font: inherit; line-height: 1.2; color: inherit; background: transparent; }}
html {{ font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; }}
html {{ background: silver; color: black; border-width: 0 0.2em 0.2em 0; }}
body {{ padding: 2em 2em 0; background: url({dataUri}) no-repeat 99.8392283% 1px white; border: solid 1px black; margin: -0.2em 0 0 -0.2em; }}");

        // Test 6: Check if border: 1px blue breaks things
        CheckBg("6-border-issue", 
            $"* {{ border: 1px blue; background: transparent; }} body {{ background: url({dataUri}) no-repeat 99.8392283% 1px white; }}");
        
        // Test 7: Without border
        CheckBg("7-no-border", 
            $"* {{ background: transparent; }} body {{ background: url({dataUri}) no-repeat 99.8392283% 1px white; }}");
            
        // Test 8: With simple data URI (no percent encoding)
        CheckBg("8-simple-uri", 
            "body { background: url(data:image/gif;base64,R0lGODlhAQABAIAAAP8AAAAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==) no-repeat 99% 1px white; }");
    }
}
