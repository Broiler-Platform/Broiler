using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class DetailedDiagnostic3
{
    private readonly ITestOutputHelper _output;
    public DetailedDiagnostic3(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Acid3_Inject_Diagnostics()
    {
        var acid3Path = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "acid", "acid3", "acid3.html"));
        Assert.True(File.Exists(acid3Path));

        var html = File.ReadAllText(acid3Path);
        var url = new Uri(acid3Path).AbsoluteUri;

        // Inject a diagnostic script that wraps the update function
        // and logs each test result to a hidden div
        var diagScript = @"<script>
var __diagResults = [];
var __origUpdate = typeof update === 'function' ? update : null;
if (__origUpdate) {
    // The acid3 tests array should already be populated
    // Let's run each test individually and log results
    var __diagDiv = document.createElement('div');
    __diagDiv.id = '__diag';
    document.body.appendChild(__diagDiv);
    
    if (typeof tests !== 'undefined') {
        for (var i = 0; i < tests.length; i++) {
            var res = 'SKIP';
            try {
                var r = tests[i]();
                if (r) {
                    res = 'FAIL:' + r;
                } else {
                    res = 'PASS';
                }
            } catch(e) {
                res = 'ERR:' + e.message;
            }
            __diagResults.push(i + ':' + res);
        }
        __diagDiv.textContent = __diagResults.join('|');
    } else {
        __diagDiv.textContent = 'NO_TESTS_ARRAY';
    }
}
</script>";

        // Insert diag script BEFORE the closing </body> but AFTER all other scripts
        html = html.Replace("</body>", diagScript + "\n</body>");
        
        var result = CaptureService.ExecuteScriptsWithDom(html, url);

        var scoreMatch = Regex.Match(result, @"id=""score""[^>]*>(\d+)<");
        if (scoreMatch.Success)
            _output.WriteLine($"=== SCORE: {scoreMatch.Groups[1].Value}/100 ===");

        // Extract diagnostic results
        var diagMatch = Regex.Match(result, @"id=""__diag""[^>]*>([\s\S]*?)</div>");
        if (diagMatch.Success)
        {
            var diagText = diagMatch.Groups[1].Value;
            var entries = diagText.Split('|');
            int passes = 0, fails = 0, errors = 0, skips = 0;
            foreach (var entry in entries)
            {
                if (entry.Contains(":PASS")) passes++;
                else if (entry.Contains(":FAIL")) fails++;
                else if (entry.Contains(":ERR")) errors++;
                else if (entry.Contains(":SKIP")) skips++;
                
                // Print failures and errors
                if (entry.Contains(":FAIL") || entry.Contains(":ERR"))
                {
                    var truncated = entry.Length > 300 ? entry.Substring(0, 300) + "..." : entry;
                    _output.WriteLine(truncated);
                }
            }
            _output.WriteLine($"\nSummary: {passes} PASS, {fails} FAIL, {errors} ERR, {skips} SKIP");
        }
        else
        {
            _output.WriteLine("Diagnostic div not found in output");
            // Try to find it
            var anyDiag = result.Contains("__diag");
            _output.WriteLine($"__diag present in output: {anyDiag}");
        }
    }
}
