using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class ScoreDiagnostic
{
    private readonly ITestOutputHelper _output;
    public ScoreDiagnostic(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Print_Acid3_Score()
    {
        var acid3Path = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "acid", "acid3", "acid3.html"));
        Assert.True(File.Exists(acid3Path));

        var html = File.ReadAllText(acid3Path);
        var url = new Uri(acid3Path).AbsoluteUri;
        var result = CaptureService.ExecuteScriptsWithDom(html, url);

        var scoreMatch = Regex.Match(result, @"id=""score""[^>]*>(\d+)<");
        Assert.True(scoreMatch.Success);
        var score = int.Parse(scoreMatch.Groups[1].Value);
        _output.WriteLine($"=== ACID3 SCORE: {score}/100 ===");

        // Check buckets
        for (int i = 1; i <= 6; i++)
        {
            var bucketMatch = Regex.Match(result, $@"id=""bucket{i}""[^>]*class=""([^""]+)""");
            if (bucketMatch.Success)
                _output.WriteLine($"Bucket {i} class: {bucketMatch.Groups[1].Value}");
            else
                _output.WriteLine($"Bucket {i}: not found");
        }
    }
}
