using System;
using System.IO;
using System.Drawing;
using Xunit;
using Xunit.Abstractions;
using Broiler.HTML.Image;
using Broiler.HTML;
using Broiler.HTML.Orchestration;

namespace Broiler.Cli.Tests;

public class Acid3CascadeDiagTests
{
    private readonly ITestOutputHelper _output;
    public Acid3CascadeDiagTests(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void Inspect_Body_Box_Properties()
    {
        var dataUri = "data:image/gif;base64,iVBORw0KGgoAAAANSUhEUgAAABQAAAAUCAIAAAAC64paAAAABGdBTUEAAK%2FINwWK6QAAAAlwSFlzAAAASAAAAEgARslrPgAAABtJREFUOMtj%2FM9APmCiQO%2Bo5lHNo5pHNVNBMwAinAEnIWw89gAAACJ6VFh0U29mdHdhcmUAAHjac0zJT0pV8MxNTE8NSk1MqQQAL5wF1K4MqU0AAAAASUVORK5CYII%3D";

        var html = $@"<!DOCTYPE html><html><head><style type='text/css'>
* {{ margin: 0; border: 1px blue; padding: 0; border-spacing: 0; font: inherit; line-height: 1.2; color: inherit; background: transparent; }}
html {{ font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; }}
html {{ background: silver; color: black; border-width: 0 0.2em 0.2em 0; }}
body {{ padding: 2em 2em 0; background: url({dataUri}) no-repeat 99.8392283% 1px white; border: solid 1px black; margin: -0.2em 0 0 -0.2em; }}
</style></head><body><p id='test'>Hello</p></body></html>";

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html, null);
        
        // Access the root box
        var root = GetRootBox(container);
        if (root == null) {
            _output.WriteLine("Cannot access root box");
            return;
        }
        
        DumpBox(root, "", 0);
    }
    
    private void DumpBox(dynamic box, string indent, int depth)
    {
        if (depth > 5 || box == null) return;
        
        string tag = "?";
        try { tag = box.HtmlTag?.Name ?? "(anon)"; } catch { }
        
        string display = "?";
        try { display = box.Display; } catch { }
        
        string bgColor = "?";
        try { bgColor = box.BackgroundColor; } catch { }
        
        string bgImage = "?";
        try { bgImage = box.BackgroundImage; } catch { }
        
        string bgRepeat = "?";
        try { bgRepeat = box.BackgroundRepeat; } catch { }
        
        _output.WriteLine($"{indent}<{tag}> display={display} bg-color={bgColor} bg-image={(bgImage?.Length > 30 ? bgImage?.Substring(0,30) + "..." : bgImage)} bg-repeat={bgRepeat}");
        
        try {
            foreach (var child in box.Boxes) {
                DumpBox(child, indent + "  ", depth + 1);
            }
        } catch { }
    }
    
    private dynamic GetRootBox(HtmlContainer container)
    {
        // Use reflection to get the root box
        var type = container.GetType();
        var field = type.GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) return field.GetValue(container);
        
        // Try property
        var prop = type.GetProperty("Root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (prop != null) return prop.GetValue(container);
        
        // Try HtmlContainerInt
        var intField = type.GetField("_htmlContainerInt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (intField != null) {
            var intObj = intField.GetValue(container);
            if (intObj != null) {
                var rootField = intObj.GetType().GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (rootField != null) return rootField.GetValue(intObj);
            }
        }
        
        return null;
    }
}
