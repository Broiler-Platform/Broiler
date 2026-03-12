using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class DumpTest
{
    private readonly ITestOutputHelper _output;
    public DumpTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public void DumpAcid3SerializedHtml()
    {
        var acid3Path = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "acid", "acid3", "acid3.html"));
        var html = File.ReadAllText(acid3Path);
        var url = new Uri(acid3Path).AbsoluteUri;
        var result = CaptureService.ExecuteScriptsWithDom(html, url);
        
        File.WriteAllText("/tmp/acid3_serialized.html", result);
        _output.WriteLine($"Written {result.Length} chars");

        var scoreMatch = Regex.Match(result, @"id=""score""[^>]*>(\d+)<");
        _output.WriteLine($"Score: {(scoreMatch.Success ? scoreMatch.Groups[1].Value : "NOT FOUND")}");
        
        foreach (Match m in Regex.Matches(result, @"id=""bucket(\d)""[^>]*class=""([^""]*)"""))
            _output.WriteLine($"Bucket {m.Groups[1].Value}: class='{m.Groups[2].Value}'");
            
        // Extract the <style> contents to check CSS
        foreach (Match m in Regex.Matches(result, @"<style[^>]*>(.*?)</style>", RegexOptions.Singleline))
        {
            var cssContent = m.Groups[1].Value;
            if (cssContent.Length > 200) cssContent = cssContent.Substring(0, 200) + "...";
            _output.WriteLine($"Style block: {cssContent}");
        }
        
        // Check for the score/result elements
        var resultMatch = Regex.Match(result, @"id=""result""[^>]*>(.*?)</div>", RegexOptions.Singleline);
        if (resultMatch.Success)
            _output.WriteLine($"Result div content: '{resultMatch.Groups[1].Value.Substring(0, Math.Min(200, resultMatch.Groups[1].Value.Length))}'");

        Assert.True(true);
    }
}
