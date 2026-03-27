using System;
using System.IO;
using System.Reflection;
using System.Drawing;
using Xunit;
using Xunit.Abstractions;
using Broiler.HTML.Image;
using SkiaSharp;

namespace Broiler.Cli.Tests;

public class Acid3CascadeDiag3Tests
{
    private readonly ITestOutputHelper _output;
    public Acid3CascadeDiag3Tests(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void Inspect_Body_Box_Via_Reflection()
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

        // Get HtmlContainerInt via reflection
        var containerType = container.GetType();
        object containerInt = null;
        foreach (var f in containerType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)) {
            _output.WriteLine($"Field: {f.Name} ({f.FieldType.Name})");
            if (f.FieldType.Name == "HtmlContainerInt") {
                containerInt = f.GetValue(container);
            }
        }
        
        if (containerInt == null) {
            _output.WriteLine("Could not find HtmlContainerInt");
            return;
        }
        
        // Get Root
        var intType = containerInt.GetType();
        var rootProp = intType.GetProperty("Root", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var root = rootProp?.GetValue(containerInt);
        _output.WriteLine($"\nRoot: {root?.GetType().Name}");
        
        if (root != null) {
            DumpBoxTree(root, "", 0);
        }
    }
    
    private void DumpBoxTree(object box, string indent, int depth)
    {
        if (depth > 6 || box == null) return;
        var type = box.GetType();
        
        string tagName = "(anon)";
        var htmlTagProp = type.GetProperty("HtmlTag", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var htmlTag = htmlTagProp?.GetValue(box);
        if (htmlTag != null) {
            var nameProp = htmlTag.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
            tagName = nameProp?.GetValue(htmlTag)?.ToString() ?? "(anon)";
        }
        
        string GetProp(string name) {
            var p = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return p?.GetValue(box)?.ToString() ?? "?";
        }
        
        var bgImg = GetProp("BackgroundImage");
        if (bgImg.Length > 50) bgImg = bgImg.Substring(0, 50) + "...";
        
        _output.WriteLine($"{indent}<{tagName}> display={GetProp("Display")} bg-color=\"{GetProp("BackgroundColor")}\" bg-repeat=\"{GetProp("BackgroundRepeat")}\" bg-image=\"{bgImg}\"");
        
        var boxesProp = type.GetProperty("Boxes", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (boxesProp != null) {
            var boxes = boxesProp.GetValue(box) as System.Collections.IEnumerable;
            if (boxes != null) {
                foreach (var child in boxes) {
                    DumpBoxTree(child, indent + "  ", depth + 1);
                }
            }
        }
    }
}
