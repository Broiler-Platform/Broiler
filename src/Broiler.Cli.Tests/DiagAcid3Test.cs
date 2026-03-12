using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;
using Broiler.Cli;

namespace Broiler.Cli.Tests;

public class DiagAcid3Test
{
    private readonly ITestOutputHelper _output;
    public DiagAcid3Test(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void DumpRenderedHtml()
    {
        var acid3Path = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
            "acid", "acid3", "acid3.html"));
        var html = File.ReadAllText(acid3Path);
        var url = "http://acid3.acidtests.org/acid3.html";
        var result = CaptureService.ExecuteScriptsWithDom(html, url);
        
        // Save to file
        File.WriteAllText("/tmp/acid3-rendered.html", result);
        _output.WriteLine($"Rendered HTML: {result.Length} bytes saved to /tmp/acid3-rendered.html");
        
        var scoreMatch = Regex.Match(result, @"id=""score""[^>]*>(\d+)<");
        _output.WriteLine($"Score found: {scoreMatch.Success}");
        if (scoreMatch.Success)
            _output.WriteLine($"Score value: {scoreMatch.Groups[1].Value}");
        
        // Show score area context
        var idx = result.IndexOf("id=\"score\"");
        if (idx >= 0)
        {
            var start = Math.Max(0, idx - 100);
            var end = Math.Min(result.Length, idx + 200);
            _output.WriteLine($"Score context: {result.Substring(start, end - start)}");
        }
        
        // Show bucket elements
        var buckets = Regex.Matches(result, @"<p\s+id=""bucket\d""[^>]*>(?:.*?)</p>", RegexOptions.Singleline);
        foreach (Match m in buckets)
            _output.WriteLine($"BUCKET: {m.Value.Substring(0, Math.Min(300, m.Value.Length))}");
        
        // Show style blocks (just the first 500 chars of each)
        var styles = Regex.Matches(result, @"<style[^>]*>([\s\S]*?)</style>", RegexOptions.IgnoreCase);
        _output.WriteLine($"Style blocks: {styles.Count}");
        foreach (Match s in styles)
        {
            var content = s.Groups[1].Value;
            _output.WriteLine($"STYLE ({content.Length} chars): {content.Substring(0, Math.Min(200, content.Length))}...");
        }
    }
}
