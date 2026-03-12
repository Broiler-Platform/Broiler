using Xunit;
using Xunit.Abstractions;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Broiler.Cli;

public class DiagnosticAcid3Test
{
    private readonly ITestOutputHelper _output;
    public DiagnosticAcid3Test(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void Acid3_Diagnostic_Which_Tests_Fail()
    {
        var acid3Path = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "acid", "acid3", "acid3.html"));
        Assert.True(File.Exists(acid3Path), $"Acid3 test file not found at {acid3Path}");

        var html = File.ReadAllText(acid3Path);
        var url = new Uri(acid3Path).AbsoluteUri;

        // Patch the acid3 harness to log test results
        // The update() function runs tests[index]() and catches errors
        // We inject a global log that records pass/fail for each test
        var patchScript = @"
<script>
var __acid3_log = [];
var __acid3_errors = [];
// Override the update function to capture results
var _origUpdate = update;
var _testIndex = 0;
function __runAllTestsSync() {
    for (var i = 0; i < tests.length; i++) {
        try {
            var result = tests[i]();
            if (result && result !== 'retry') {
                __acid3_log.push('PASS:' + i);
            } else {
                __acid3_log.push('FAIL:' + i + ':returned=' + result);
            }
        } catch(e) {
            __acid3_log.push('FAIL:' + i + ':' + (e.message || e));
            __acid3_errors.push('TEST_' + i + ':' + (e.message || e));
        }
    }
    var logEl = document.createElement('div');
    logEl.id = '__acid3_diag';
    logEl.textContent = __acid3_log.join('|') + '|||ERRORS:' + __acid3_errors.join('|');
    document.body.appendChild(logEl);
}
__runAllTestsSync();
</script>";

        // Inject the patch before </body>
        html = html.Replace("</body>", patchScript + "\n</body>");

        var result = CaptureService.ExecuteScriptsWithDom(html, url);

        // Extract diagnostic output
        var diagMatch = Regex.Match(result, @"id=""__acid3_diag""[^>]*>(.*?)</div>", RegexOptions.Singleline);
        if (diagMatch.Success)
        {
            var diagContent = System.Net.WebUtility.HtmlDecode(diagMatch.Groups[1].Value);
            var parts = diagContent.Split("|||");
            var testResults = parts[0].Split('|');
            
            int passCount = 0;
            int failCount = 0;
            foreach (var tr in testResults)
            {
                if (tr.StartsWith("FAIL:"))
                {
                    _output.WriteLine(tr);
                    failCount++;
                }
                else if (tr.StartsWith("PASS:"))
                {
                    passCount++;
                }
            }
            _output.WriteLine($"\nTotal: {passCount} passed, {failCount} failed out of {testResults.Length}");
            
            if (parts.Length > 1)
            {
                _output.WriteLine($"\nERRORS: {parts[1]}");
            }
        }
        else
        {
            _output.WriteLine("Could not find diagnostic output in result");
            // Try to find any sign of it
            if (result.Contains("__acid3_diag"))
                _output.WriteLine("Found __acid3_diag element but couldn't parse it");
        }
        
        // Also show score
        var scoreMatch = Regex.Match(result, @"id=""score""[^>]*>(\d+)<");
        if (scoreMatch.Success)
            _output.WriteLine($"\nFinal score: {scoreMatch.Groups[1].Value}/100");
    }
}
