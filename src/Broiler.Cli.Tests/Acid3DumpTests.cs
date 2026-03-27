using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class Acid3DumpTests
{
    private readonly ITestOutputHelper _output;
    public Acid3DumpTests(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void Dump_Acid3_Processed_Html()
    {
        var html = File.ReadAllText("/home/runner/work/Broiler/Broiler/acid/acid3/acid3.html");
        var processed = CaptureService.ExecuteScriptsWithDom(html, 
            "file:///home/runner/work/Broiler/Broiler/acid/acid3/acid3.html",
            "/home/runner/work/Broiler/Broiler/acid/acid3");
        processed = Broiler.App.Rendering.HtmlPostProcessor.Process(processed);
        File.WriteAllText("/tmp/acid3-processed.html", processed);
        
        // Extract just the CSS from style tags
        var styleStart = processed.IndexOf("<style");
        while (styleStart >= 0)
        {
            var styleEnd = processed.IndexOf("</style>", styleStart);
            if (styleEnd < 0) break;
            var block = processed[styleStart..(styleEnd + 8)];
            _output.WriteLine($"=== STYLE BLOCK (at pos {styleStart}) ===");
            _output.WriteLine(block.Length > 2000 ? block[..2000] + "..." : block);
            styleStart = processed.IndexOf("<style", styleEnd);
        }
        
        _output.WriteLine($"\n=== Total HTML length: {processed.Length} ===");
        
        // Check for "background" in CSS 
        var bgIdx = processed.IndexOf("background:", StringComparison.OrdinalIgnoreCase);
        while (bgIdx >= 0 && bgIdx < processed.Length)
        {
            var lineEnd = processed.IndexOf('\n', bgIdx);
            if (lineEnd < 0) lineEnd = Math.Min(bgIdx + 200, processed.Length);
            var line = processed[bgIdx..Math.Min(lineEnd, bgIdx + 300)];
            _output.WriteLine($"\n=== background at pos {bgIdx}: {line.Trim()} ===");
            bgIdx = processed.IndexOf("background:", bgIdx + 1, StringComparison.OrdinalIgnoreCase);
        }
    }
}
