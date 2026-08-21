using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Covers the configurable pixel pass threshold: how the percentage is resolved from the
/// command line/environment, how it maps onto the diff ratio <c>PixelDiffRunner</c>
/// compares against, and that a run actually passes or fails a test by that bar.
/// </summary>
public sealed class WptPassThresholdTests : IDisposable
{
    private const string PassThresholdEnvironmentVariable = "BROILER_WPT_PASS_THRESHOLD";

    private readonly string _tempDir;
    private readonly string? _originalEnvironmentValue;

    public WptPassThresholdTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-wpt-threshold-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _originalEnvironmentValue = Environment.GetEnvironmentVariable(PassThresholdEnvironmentVariable);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(PassThresholdEnvironmentVariable, _originalEnvironmentValue);

        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leaving a directory under %TEMP% behind is not a test failure.
        }
    }

    [Fact(Timeout = 600000)]
    public void Pass_Threshold_Defaults_To_99_Percent()
    {
        Environment.SetEnvironmentVariable(PassThresholdEnvironmentVariable, null);

        Assert.True(Program.TryResolvePassThreshold(null, out var percent, out var error));
        Assert.Null(error);
        Assert.Equal(99, percent);
        Assert.Equal(99, WptTestRunner.DefaultPassThresholdPercent);
    }

    [Fact(Timeout = 600000)]
    public void Command_Line_Pass_Threshold_Wins_Over_The_Environment()
    {
        Environment.SetEnvironmentVariable(PassThresholdEnvironmentVariable, "90");

        Assert.True(Program.TryResolvePassThreshold(97.5, out var percent, out _));
        Assert.Equal(97.5, percent);
    }

    [Fact(Timeout = 600000)]
    public void Environment_Pass_Threshold_Is_Used_When_No_Argument_Is_Given()
    {
        Environment.SetEnvironmentVariable(PassThresholdEnvironmentVariable, "95");

        Assert.True(Program.TryResolvePassThreshold(null, out var percent, out _));
        Assert.Equal(95, percent);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("-1")]
    [InlineData("101")]
    public void Invalid_Environment_Pass_Threshold_Is_Rejected(string value)
    {
        Environment.SetEnvironmentVariable(PassThresholdEnvironmentVariable, value);

        Assert.False(Program.TryResolvePassThreshold(null, out _, out var error));
        Assert.Contains(PassThresholdEnvironmentVariable, error);
    }

    [Theory]
    [InlineData(99, 0.01)]
    [InlineData(100, 0.0)]
    [InlineData(95, 0.05)]
    [InlineData(0, 1.0)]
    public void Percent_Threshold_Maps_Onto_The_Diff_Ratio(double percent, double expectedRatio)
    {
        var config = WptTestRunner.CreatePixelDiffConfig(percent);

        Assert.Equal(expectedRatio, config.PixelDiffThreshold, precision: 10);
    }

    [Fact(Timeout = 600000)]
    public void A_Two_Percent_Mismatch_Fails_At_99_But_Passes_At_97()
    {
        const int width = 200;
        const int height = 100;
        const int differingPixels = (width * height) / 50; // exactly 2% of the canvas

        var testFile = Path.Combine(_tempDir, "pass-threshold.html");
        File.WriteAllText(testFile,
            "<!DOCTYPE html><html><body style=\"margin:0\">" +
            "<div style=\"width:200px;height:100px;background:white\"></div></body></html>");

        var referenceDir = Path.Combine(_tempDir, "references");
        Directory.CreateDirectory(referenceDir);

        // The reference is this build's own render with a known slice of its pixels
        // repainted, so the diff ratio is exactly `differingPixels / (width * height)`
        // and the assertions do not depend on how the page itself renders.
        var strictRunner = new WptTestRunner(width, height, passThresholdPercent: 99);
        using (var rendered = strictRunner.RenderHtmlFileBitmapPublic(testFile, null))
        using (var reference = rendered.Copy())
        {
            for (int i = 0; i < differingPixels; i++)
                reference.SetPixel(i % width, i / width, new Graphics.BColor(255, 0, 255, 255));

            reference.Save(Path.Combine(referenceDir, "pass-threshold.png"));
        }

        var strictResult = strictRunner.RunTest(testFile, referenceDir);
        Assert.False(strictResult.Passed);
        Assert.Equal(FailureCategory.PixelMismatch, strictResult.Category);
        Assert.Equal(98, strictResult.MatchPercent!.Value, precision: 3);

        var lenientResult = new WptTestRunner(width, height, passThresholdPercent: 97)
            .RunTest(testFile, referenceDir);
        Assert.True(lenientResult.Passed, lenientResult.Message);
        Assert.Equal(98, lenientResult.MatchPercent!.Value, precision: 3);
    }
}
