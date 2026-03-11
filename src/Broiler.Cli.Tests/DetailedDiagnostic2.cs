using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class DetailedDiagnostic2
{
    private readonly ITestOutputHelper _output;
    public DetailedDiagnostic2(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Acid3_Detailed_TestByTest()
    {
        // Load real acid3 but inject diagnostic wrapper
        var acid3Path = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "acid", "acid3", "acid3.html"));
        Assert.True(File.Exists(acid3Path));

        var html = @"<!DOCTYPE html>
<html><head><title>Acid3 Diag</title></head>
<body>
<span id=""score"">0</span>
<span id=""log""></span>
<script>" + File.ReadAllText(acid3Path).Split(new[] { "// test 0" }, StringSplitOptions.None).Length.ToString() + @"</script>
</body></html>";

        // Instead, let's use the real acid3.html and capture console output
        html = File.ReadAllText(acid3Path);
        var url = new Uri(acid3Path).AbsoluteUri;

        // We need to intercept the Acid3 update() to log results.
        // Let's inject a script that wraps each test call
        // The Acid3 harness has: if (result) { ... fail ... } else { score++ }
        // where result is a string for failure, undefined for success
        // We can wrap the update function.
        
        // Actually, let's look at the rendered output and look for the log div
        var result = CaptureService.ExecuteScriptsWithDom(html, url);

        var scoreMatch = Regex.Match(result, @"id=""score""[^>]*>(\d+)<");
        if (scoreMatch.Success)
            _output.WriteLine($"=== SCORE: {scoreMatch.Groups[1].Value}/100 ===");

        // Find log element - Acid3 logs failures in a specific element
        // The log element captures test failure messages
        var logMatch = Regex.Match(result, @"id=""log""[^>]*>([\s\S]*?)</ul>", RegexOptions.Singleline);
        if (logMatch.Success)
        {
            var logContent = logMatch.Groups[1].Value;
            // Extract individual failure messages
            var liMatches = Regex.Matches(logContent, @"<li[^>]*>([\s\S]*?)</li>");
            _output.WriteLine($"Failure entries: {liMatches.Count}");
            foreach (Match li in liMatches)
            {
                var text = Regex.Replace(li.Groups[1].Value, @"<[^>]+>", "").Trim();
                if (text.Length > 200) text = text.Substring(0, 200) + "...";
                _output.WriteLine($"  FAIL: {text}");
            }
        }
        else
        {
            _output.WriteLine("No log element found with </ul>");
            // Try alternative
            logMatch = Regex.Match(result, @"id=""log""[^>]*>([\s\S]{0,5000})");
            if (logMatch.Success)
                _output.WriteLine($"Log raw: {logMatch.Groups[1].Value.Substring(0, Math.Min(3000, logMatch.Groups[1].Value.Length))}");
        }
    }
}
