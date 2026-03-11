// Temporary diagnostic - inject into test suite to get per-test failure details
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;
using Broiler.Cli;

namespace Broiler.Cli.Tests
{
    public class DiagnosticTest
    {
        private readonly ITestOutputHelper _output;
        public DiagnosticTest(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void Acid3_Detailed_Diagnosis()
        {
            var acid3Path = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "acid", "acid3", "acid3.html"));
            Assert.True(File.Exists(acid3Path), $"Acid3 test file not found at {acid3Path}");
            var html = File.ReadAllText(acid3Path);
            var url = new Uri(acid3Path).AbsoluteUri;
            var result = CaptureService.ExecuteScriptsWithDom(html, url);

            // Extract score
            var scoreMatch = Regex.Match(result, @"id=""score""[^>]*>(\d+)<");
            if (scoreMatch.Success)
                _output.WriteLine($"SCORE: {scoreMatch.Groups[1].Value}");
            else
                _output.WriteLine("SCORE: Could not extract");

            // Look for bucket elements and their classes
            var bucketPattern = new Regex(@"id=""(bucket\d+)""[^>]*class=""([^""]*)""");
            foreach (Match m in bucketPattern.Matches(result))
                _output.WriteLine($"BUCKET: {m.Groups[1].Value} class={m.Groups[2].Value}");

            // Look for any error messages or fail indicators
            // The acid3 harness puts failures in d1-d5 text nodes
            var dPattern = new Regex(@"id=""(d\d+)""[^>]*>([^<]*)<");
            foreach (Match m in dPattern.Matches(result))
                _output.WriteLine($"DIAGNOSTIC: {m.Groups[1].Value} = {m.Groups[2].Value}");

            // Output length for reference
            _output.WriteLine($"OUTPUT LENGTH: {result.Length}");
            
            // Check for FAIL text
            var failCount = Regex.Matches(result, @"FAIL", RegexOptions.IgnoreCase).Count;
            _output.WriteLine($"FAIL occurrences: {failCount}");
        }
    }
}
