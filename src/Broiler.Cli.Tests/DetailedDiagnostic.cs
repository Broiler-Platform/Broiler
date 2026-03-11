using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class DetailedDiagnostic
{
    private readonly ITestOutputHelper _output;
    public DetailedDiagnostic(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Print_Acid3_Details()
    {
        var acid3Path = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "acid", "acid3", "acid3.html"));
        Assert.True(File.Exists(acid3Path));

        var html = File.ReadAllText(acid3Path);
        var url = new Uri(acid3Path).AbsoluteUri;

        // Inject diagnostic code after the update() function definition
        // to capture which tests pass and which fail
        var diagScript = @"
<script>
// Override to capture individual test results
var _origTests = typeof tests !== 'undefined' ? tests.slice() : [];
var _testResults = [];
</script>";
        html = html.Replace("</body>", diagScript + "</body>");

        var result = CaptureService.ExecuteScriptsWithDom(html, url);

        var scoreMatch = Regex.Match(result, @"id=""score""[^>]*>(\d+)<");
        if (scoreMatch.Success)
            _output.WriteLine($"SCORE: {scoreMatch.Groups[1].Value}/100");

        // Check for error indicators  
        var failMatches = Regex.Matches(result, @"FAIL");
        _output.WriteLine($"FAIL occurrences in output: {failMatches.Count}");

        // Look for the test log element
        var logMatch = Regex.Match(result, @"id=""log""[^>]*>(.*?)</div>", RegexOptions.Singleline);
        if (logMatch.Success)
            _output.WriteLine($"Log content: {logMatch.Groups[1].Value.Substring(0, Math.Min(2000, logMatch.Groups[1].Value.Length))}");
        
        // Look for any element with "FAIL" text
        var failElements = Regex.Matches(result, @"FAIL:([^<]{0,200})");
        foreach (Match m in failElements)
            _output.WriteLine($"FAIL text: {m.Value}");
    }
}
