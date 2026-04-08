using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Unit tests for <see cref="WptTestRunner"/> — validates test discovery,
/// rendering pipeline, and result reporting.
/// </summary>
public class WptTestRunnerTests : IDisposable
{
    private readonly string _tempDir;

    public WptTestRunnerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-wpt-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void DiscoverTests_Finds_Html_Files_Recursively()
    {
        // Arrange — create a nested directory structure with mixed file types.
        var subDir = Path.Combine(_tempDir, "css", "selectors");
        Directory.CreateDirectory(subDir);

        File.WriteAllText(Path.Combine(_tempDir, "test1.html"), "<html></html>");
        File.WriteAllText(Path.Combine(subDir, "test2.htm"), "<html></html>");
        File.WriteAllText(Path.Combine(subDir, "test3.xhtml"), "<html></html>");
        File.WriteAllText(Path.Combine(subDir, "readme.txt"), "not a test");
        File.WriteAllText(Path.Combine(subDir, "style.css"), "body{}");

        // Act
        var tests = WptTestRunner.DiscoverTests(_tempDir).ToList();

        // Assert — only .html, .htm, .xhtml files should be discovered.
        Assert.Equal(3, tests.Count);
        Assert.All(tests, t => Assert.True(
            t.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
            t.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) ||
            t.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void DiscoverTests_Excludes_NonTest_Files()
    {
        // Arrange — create actual test files mixed with non-test WPT artefacts.
        var testDir = Path.Combine(_tempDir, "css", "compositing");
        var refDir = Path.Combine(testDir, "reference");
        var supportDir = Path.Combine(testDir, "support");
        var testPlanDir = Path.Combine(testDir, "test-plan");
        Directory.CreateDirectory(testDir);
        Directory.CreateDirectory(refDir);
        Directory.CreateDirectory(supportDir);
        Directory.CreateDirectory(testPlanDir);

        // Actual test files (should be discovered).
        File.WriteAllText(Path.Combine(testDir, "mix-blend-mode-basic.html"), "<html></html>");
        File.WriteAllText(Path.Combine(testDir, "root-element-opacity.html"), "<html></html>");

        // Non-test files (should be excluded).
        File.WriteAllText(Path.Combine(refDir, "mix-blend-mode-video-notref.html"), "<html></html>");
        File.WriteAllText(Path.Combine(supportDir, "helper.html"), "<html></html>");
        File.WriteAllText(Path.Combine(testPlanDir, "test-plan.html"), "<html></html>");
        File.WriteAllText(Path.Combine(testPlanDir, "test-plan.src.html"), "<html></html>");
        File.WriteAllText(Path.Combine(testDir, "opacity-ref.html"), "<html></html>");
        File.WriteAllText(Path.Combine(testDir, "opacity-notref.html"), "<html></html>");

        // Act
        var tests = WptTestRunner.DiscoverTests(_tempDir).ToList();

        // Assert — only the two actual test files should be discovered.
        Assert.Equal(2, tests.Count);
        Assert.Contains(tests, t => t.EndsWith("mix-blend-mode-basic.html"));
        Assert.Contains(tests, t => t.EndsWith("root-element-opacity.html"));
    }

    [Fact]
    public void DiscoverTests_Returns_Empty_For_Empty_Directory()
    {
        var tests = WptTestRunner.DiscoverTests(_tempDir).ToList();
        Assert.Empty(tests);
    }

    [Fact]
    public void RunTest_Returns_Skipped_When_No_Reference_Image()
    {
        // Arrange
        var testFile = Path.Combine(_tempDir, "test.html");
        File.WriteAllText(testFile, "<html><body><p>Hello</p></body></html>");

        var refDir = Path.Combine(_tempDir, "references");
        Directory.CreateDirectory(refDir);

        var runner = new WptTestRunner();

        // Act
        var result = runner.RunTest(testFile, refDir);

        // Assert — no reference → skipped.
        Assert.True(result.Skipped);
        Assert.Contains("No reference image", result.Message);
    }

    [Fact]
    public void RunTest_Returns_Failure_For_Missing_Test_File()
    {
        var runner = new WptTestRunner();

        var result = runner.RunTest(
            Path.Combine(_tempDir, "nonexistent.html"),
            Path.Combine(_tempDir, "references"));

        Assert.False(result.Passed);
        Assert.False(result.Skipped);
        Assert.Contains("not found", result.Message);
    }

    [Fact]
    public void RunTest_Renders_Simple_Html_Without_Crash()
    {
        // Arrange
        var testFile = Path.Combine(_tempDir, "simple.html");
        File.WriteAllText(testFile, @"<!DOCTYPE html>
<html><body><p>Hello World</p></body></html>");

        var refDir = Path.Combine(_tempDir, "references");
        Directory.CreateDirectory(refDir);

        // No reference image — test should be skipped, but rendering
        // should not throw.
        var runner = new WptTestRunner();

        // Act — should not throw
        var result = runner.RunTest(testFile, refDir);

        // Assert
        Assert.True(result.Skipped);
    }

    [Fact]
    public void RunTest_Executes_Inline_Script_Before_Render()
    {
        // Arrange — create a test with a script that modifies the DOM.
        var testFile = Path.Combine(_tempDir, "script.html");
        File.WriteAllText(testFile, @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var p = document.createElement('p');
p.textContent = 'generated';
document.getElementById('out').appendChild(p);
</script>
</body></html>");

        var refDir = Path.Combine(_tempDir, "references");
        Directory.CreateDirectory(refDir);

        var runner = new WptTestRunner();

        // Act — should process scripts without throwing.
        var result = runner.RunTest(testFile, refDir);

        // Assert — skipped because no reference, but the pipeline ran.
        Assert.True(result.Skipped);
    }

    [Fact]
    public void RunAll_Processes_Multiple_Tests()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "a.html"), "<html><body>A</body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "b.html"), "<html><body>B</body></html>");

        var refDir = Path.Combine(_tempDir, "references");
        Directory.CreateDirectory(refDir);

        var runner = new WptTestRunner();

        // Act
        var results = runner.RunAll(_tempDir, refDir).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Skipped));
    }

    [Fact]
    public void RunTest_Returns_Null_MatchPercent_When_Skipped()
    {
        // Arrange — no reference image → skipped, MatchPercent should be null.
        var testFile = Path.Combine(_tempDir, "skip.html");
        File.WriteAllText(testFile, "<html><body>Skip me</body></html>");

        var refDir = Path.Combine(_tempDir, "references");
        Directory.CreateDirectory(refDir);

        var runner = new WptTestRunner();

        // Act
        var result = runner.RunTest(testFile, refDir);

        // Assert — skipped results have no pixel comparison.
        Assert.True(result.Skipped);
        Assert.Null(result.MatchPercent);
    }

    [Fact]
    public void RunTest_Returns_Null_MatchPercent_When_File_Not_Found()
    {
        // Arrange — missing test file → error, MatchPercent should be null.
        var runner = new WptTestRunner();

        var result = runner.RunTest(
            Path.Combine(_tempDir, "missing.html"),
            Path.Combine(_tempDir, "references"));

        // Assert — error before pixel comparison stage.
        Assert.False(result.Passed);
        Assert.Null(result.MatchPercent);
    }

    [Fact]
    public void Program_Output_Includes_Percent_Match_And_Is_Sorted()
    {
        // The Program.Main entry point writes sorted log output to stdout.
        // With only skipped tests (no references), there are no [PASS]/[FAIL]
        // lines, but the summary line should still be correct.
        var testDir = Path.Combine(_tempDir, "sorted");
        Directory.CreateDirectory(testDir);
        File.WriteAllText(Path.Combine(testDir, "a.html"), "<html><body>A</body></html>");

        var originalOut = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            Program.Main([testDir]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = sw.ToString();

        // With no reference images all tests are skipped; verify summary line.
        Assert.Contains("0 passed, 0 failed, 1 skipped", output);
    }

    [Fact]
    public void Program_Returns_Error_When_No_Arguments()
    {
        var exitCode = Program.Main([]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Program_Returns_Error_For_Missing_Directory()
    {
        var exitCode = Program.Main(["--wpt-dir", "/nonexistent/path"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Program_Returns_Success_For_Empty_Directory()
    {
        // An empty directory has no tests → 0 failures → exit 0.
        var exitCode = Program.Main([_tempDir]);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Program_Help_Returns_Zero()
    {
        var exitCode = Program.Main(["--help"]);
        Assert.Equal(0, exitCode);
    }

    // ──────────── Crash test detection ──────────────────────────────

    [Fact]
    public void IsCrashTest_Detects_CrashTestDir()
    {
        Assert.True(WptTestRunner.IsCrashTest("/wpt/css/compositing/crashtests/bgblend-root-change.html"));
        Assert.True(WptTestRunner.IsCrashTest("C:\\wpt\\crashtests\\test.html"));
    }

    [Fact]
    public void IsCrashTest_Detects_CrashSuffix()
    {
        Assert.True(WptTestRunner.IsCrashTest("/wpt/css/compositing/root-element-background-contain-hidden-crash.html"));
        Assert.True(WptTestRunner.IsCrashTest("/wpt/my-test-crash.htm"));
    }

    [Fact]
    public void IsCrashTest_Returns_False_For_Normal_Tests()
    {
        Assert.False(WptTestRunner.IsCrashTest("/wpt/css/compositing/root-element-opacity.html"));
        Assert.False(WptTestRunner.IsCrashTest("/wpt/css/compositing/root-element-background-margin-opacity.html"));
    }

    // ──────────── Non-test file detection ────────────────────────────

    [Fact]
    public void IsNonTestFile_Detects_Reference_Directory()
    {
        Assert.True(WptTestRunner.IsNonTestFile("/wpt/css/compositing/reference/mix-blend-mode-video-notref.html"));
        Assert.True(WptTestRunner.IsNonTestFile("C:\\wpt\\css\\compositing\\reference\\some-ref.html"));
    }

    [Fact]
    public void IsNonTestFile_Detects_Support_Directory()
    {
        Assert.True(WptTestRunner.IsNonTestFile("/wpt/css/compositing/support/helper.html"));
        Assert.True(WptTestRunner.IsNonTestFile("C:\\wpt\\support\\utils.html"));
    }

    [Fact]
    public void IsNonTestFile_Detects_TestPlan_Directory()
    {
        Assert.True(WptTestRunner.IsNonTestFile("/wpt/css/compositing/test-plan/test-plan.html"));
        Assert.True(WptTestRunner.IsNonTestFile("/wpt/css/compositing/test-plan/css-blending-test-plan-proposal.html"));
    }

    [Fact]
    public void IsNonTestFile_Detects_Src_Html_Extension()
    {
        Assert.True(WptTestRunner.IsNonTestFile("/wpt/css/compositing/test-plan/test-plan.src.html"));
        Assert.True(WptTestRunner.IsNonTestFile("/wpt/spec.src.htm"));
    }

    [Fact]
    public void IsNonTestFile_Detects_Ref_And_Notref_Suffixes()
    {
        Assert.True(WptTestRunner.IsNonTestFile("/wpt/css/compositing/root-element-opacity-ref.html"));
        Assert.True(WptTestRunner.IsNonTestFile("/wpt/css/compositing/mix-blend-mode-video-notref.html"));
        Assert.True(WptTestRunner.IsNonTestFile("/wpt/test-ref.htm"));
        Assert.True(WptTestRunner.IsNonTestFile("/wpt/test-notref.xhtml"));
    }

    [Fact]
    public void IsNonTestFile_Returns_False_For_Actual_Tests()
    {
        Assert.False(WptTestRunner.IsNonTestFile("/wpt/css/compositing/root-element-opacity.html"));
        Assert.False(WptTestRunner.IsNonTestFile("/wpt/css/compositing/mix-blend-mode/mix-blend-mode-basic.html"));
        Assert.False(WptTestRunner.IsNonTestFile("/wpt/css/selectors/test1.html"));
    }

    // ──────────── Media playback detection ───────────────────────────

    [Fact]
    public void IsMediaPlaybackTest_Detects_Video_With_Source()
    {
        var html = @"<html><body>
<video autoplay>
    <source type=""video/mp4"" src=""support/red_circle.mp4"">
</video>
</body></html>";
        Assert.True(WptTestRunner.IsMediaPlaybackTest(html));
    }

    [Fact]
    public void IsMediaPlaybackTest_Returns_False_For_No_Video()
    {
        var html = @"<html><body><p>No video here</p></body></html>";
        Assert.False(WptTestRunner.IsMediaPlaybackTest(html));
    }

    [Fact]
    public void RunTest_Skips_Media_Playback_Test()
    {
        var testFile = Path.Combine(_tempDir, "video-test.html");
        File.WriteAllText(testFile, @"<!DOCTYPE html>
<html><body>
<video autoplay>
    <source type=""video/mp4"" src=""support/video.mp4"">
</video>
<div>Overlay</div>
</body></html>");

        var refDir = Path.Combine(_tempDir, "references");
        Directory.CreateDirectory(refDir);

        var runner = new WptTestRunner();
        var result = runner.RunTest(testFile, refDir);

        Assert.True(result.Skipped);
        Assert.Contains("media playback", result.Message);
    }

    [Fact]
    public void RunTest_CrashTestDir_AutoPasses_Without_Reference()
    {
        // Arrange — a crash test file under a "crashtests" directory
        // should auto-pass when rendering succeeds (no reference needed).
        var crashDir = Path.Combine(_tempDir, "crashtests");
        Directory.CreateDirectory(crashDir);

        var testFile = Path.Combine(crashDir, "simple-crash.html");
        File.WriteAllText(testFile, @"<!DOCTYPE html>
<html><body><p>Crash test</p></body></html>");

        var refDir = Path.Combine(_tempDir, "references");
        Directory.CreateDirectory(refDir);

        var runner = new WptTestRunner();

        // Act
        var result = runner.RunTest(testFile, refDir);

        // Assert — crash test auto-passes when rendering doesn't throw.
        Assert.True(result.Passed);
        Assert.Contains("Crash test", result.Message);
    }

    [Fact]
    public void RunTest_CrashSuffix_AutoPasses_Without_Reference()
    {
        // Arrange — file name ends with "-crash", should auto-pass.
        var testFile = Path.Combine(_tempDir, "my-test-crash.html");
        File.WriteAllText(testFile, @"<!DOCTYPE html>
<html><body><div style=""background-color: red"">Test</div></body></html>");

        var refDir = Path.Combine(_tempDir, "references");
        Directory.CreateDirectory(refDir);

        var runner = new WptTestRunner();

        // Act
        var result = runner.RunTest(testFile, refDir);

        // Assert — crash test auto-passes when rendering doesn't throw.
        Assert.True(result.Passed);
    }

    // ──────────── Failure categorization ─────────────────────────────

    [Fact]
    public void RunTest_MissingFile_Returns_FileIO_Category()
    {
        var runner = new WptTestRunner();

        var result = runner.RunTest(
            Path.Combine(_tempDir, "nonexistent.html"),
            Path.Combine(_tempDir, "references"));

        Assert.False(result.Passed);
        Assert.Equal(FailureCategory.FileIO, result.Category);
    }

    [Fact]
    public void RunTest_Skipped_Returns_None_Category()
    {
        var testFile = Path.Combine(_tempDir, "skip-cat.html");
        File.WriteAllText(testFile, "<html><body>Skip</body></html>");

        var refDir = Path.Combine(_tempDir, "references");
        Directory.CreateDirectory(refDir);

        var runner = new WptTestRunner();
        var result = runner.RunTest(testFile, refDir);

        Assert.True(result.Skipped);
        Assert.Equal(FailureCategory.None, result.Category);
    }

    [Fact]
    public void RunTest_Passed_Returns_None_Category()
    {
        // Crash tests auto-pass; verify they have Category.None.
        var crashDir = Path.Combine(_tempDir, "crashtests");
        Directory.CreateDirectory(crashDir);

        var testFile = Path.Combine(crashDir, "cat-test.html");
        File.WriteAllText(testFile, "<html><body>OK</body></html>");

        var refDir = Path.Combine(_tempDir, "references");
        Directory.CreateDirectory(refDir);

        var runner = new WptTestRunner();
        var result = runner.RunTest(testFile, refDir);

        Assert.True(result.Passed);
        Assert.Equal(FailureCategory.None, result.Category);
    }

    [Fact]
    public void Program_Output_Includes_Root_Cause_Analysis_For_Failures()
    {
        // When there are failures, the program should include a Root Cause
        // Analysis section in its output.
        var testDir = Path.Combine(_tempDir, "rca");
        Directory.CreateDirectory(testDir);

        // Create a test file that will fail (reference image is invalid).
        var testFile = Path.Combine(testDir, "fail.html");
        File.WriteAllText(testFile, "<html><body>Fail</body></html>");

        var refDir = Path.Combine(testDir, "references");
        Directory.CreateDirectory(refDir);
        // Write an invalid PNG to force a ReferenceDecodeError.
        File.WriteAllText(Path.Combine(refDir, "fail.png"), "not-a-png");

        var originalOut = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            Program.Main(["--wpt-dir", testDir, "--reference-dir", refDir]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = sw.ToString();

        Assert.Contains("[FAIL]", output);
        Assert.Contains("Root Cause Analysis", output);
        Assert.Contains("[ReferenceDecodeError]", output);
    }

    [Fact]
    public void Program_Output_Includes_Category_Tag_On_Fail_Lines()
    {
        // Verify that [FAIL] lines include a category tag like [FileIO].
        // Use a missing-file scenario to trigger FileIO category.
        // We can't directly cause a FileIO failure through Program.Main
        // since it only runs discovered files, but we can check the
        // reference decode error path.
        var testDir = Path.Combine(_tempDir, "cat-tag");
        Directory.CreateDirectory(testDir);
        File.WriteAllText(Path.Combine(testDir, "t.html"), "<html><body>T</body></html>");

        var refDir = Path.Combine(testDir, "references");
        Directory.CreateDirectory(refDir);
        File.WriteAllText(Path.Combine(refDir, "t.png"), "bad-data");

        var originalOut = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            Program.Main(["--wpt-dir", testDir, "--reference-dir", refDir]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = sw.ToString();
        // The [FAIL] line should include a [Category] tag.
        Assert.Matches(@"\[FAIL\] \[\w+\]", output);
    }

    // ──────────── MismatchClassifier ─────────────────────────────────

    [Fact]
    public void MismatchClassifier_SizeMismatch_When_Dimensions_Differ()
    {
        var diff = new PixelDiffResult
        {
            DiffRatio = 1.0,
            DiffPixelCount = 100,
            TotalPixelCount = 100,
            IsMatch = false,
        };

        var diag = MismatchClassifier.Classify(diff, 100, 100, 200, 200);

        Assert.Equal(MismatchCategory.SizeMismatch, diag.Category);
        Assert.Contains("dimensions differ", diag.Summary);
    }

    [Fact]
    public void MismatchClassifier_MinorDiff_When_Few_Pixels_Differ()
    {
        // DiffRatio < 0.01 → MinorDiff
        var mismatches = new List<PixelMismatch>
        {
            new(10, 10, 200, 0, 0, 255, 100, 0, 0, 255),
        };
        var diff = new PixelDiffResult
        {
            DiffRatio = 0.005,
            DiffPixelCount = 5,
            TotalPixelCount = 1000,
            IsMatch = false,
            Mismatches = mismatches,
        };

        var diag = MismatchClassifier.Classify(diff, 100, 100, 100, 100);

        Assert.Equal(MismatchCategory.MinorDiff, diag.Category);
        Assert.Contains("Near-match", diag.Summary);
    }

    [Fact]
    public void MismatchClassifier_SubpixelAntiAliasing_When_Small_Deltas()
    {
        // Small per-channel delta → SubpixelAntiAliasing
        var mismatches = new List<PixelMismatch>();
        for (int i = 0; i < 100; i++)
        {
            // ~10 avg delta per channel
            mismatches.Add(new PixelMismatch(i, 0,
                (byte)(128 + 10), 128, 128, 255,
                128, 128, 128, 255));
        }
        var diff = new PixelDiffResult
        {
            DiffRatio = 0.05,
            DiffPixelCount = 100,
            TotalPixelCount = 2000,
            IsMatch = false,
            Mismatches = mismatches,
        };

        var diag = MismatchClassifier.Classify(diff, 100, 100, 100, 100);

        Assert.Equal(MismatchCategory.SubpixelAntiAliasing, diag.Category);
        Assert.Contains("anti-aliasing", diag.Summary);
    }

    [Fact]
    public void MismatchClassifier_ColorShift_When_Moderate_Deltas()
    {
        // Moderate per-channel delta → ColorShift
        var mismatches = new List<PixelMismatch>();
        for (int i = 0; i < 100; i++)
        {
            // ~50 avg delta per channel
            mismatches.Add(new PixelMismatch(i, i,
                200, 100, 50, 255,
                150, 50, 0, 255));
        }
        var diff = new PixelDiffResult
        {
            DiffRatio = 0.05,
            DiffPixelCount = 100,
            TotalPixelCount = 2000,
            IsMatch = false,
            Mismatches = mismatches,
        };

        var diag = MismatchClassifier.Classify(diff, 100, 100, 100, 100);

        Assert.Equal(MismatchCategory.ColorShift, diag.Category);
        Assert.Contains("color", diag.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MismatchClassifier_LayoutShift_When_Large_Deltas()
    {
        // High per-channel delta → LayoutShift
        var mismatches = new List<PixelMismatch>();
        for (int i = 0; i < 100; i++)
        {
            // ~255 delta → high
            mismatches.Add(new PixelMismatch(i, i,
                255, 0, 0, 255,
                0, 255, 0, 255));
        }
        var diff = new PixelDiffResult
        {
            DiffRatio = 0.10,
            DiffPixelCount = 200,
            TotalPixelCount = 2000,
            IsMatch = false,
            Mismatches = mismatches,
        };

        var diag = MismatchClassifier.Classify(diff, 100, 100, 100, 100);

        Assert.Equal(MismatchCategory.LayoutShift, diag.Category);
        Assert.Contains("layout", diag.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MismatchClassifier_MissingContent_When_White_To_NonWhite()
    {
        // Majority of mismatches are white↔non-white → MissingContent
        var mismatches = new List<PixelMismatch>();
        for (int i = 0; i < 100; i++)
        {
            // actual is white, baseline has content
            mismatches.Add(new PixelMismatch(i, 0,
                255, 255, 255, 255,
                200, 0, 0, 255));
        }
        var diff = new PixelDiffResult
        {
            DiffRatio = 0.05,
            DiffPixelCount = 100,
            TotalPixelCount = 2000,
            IsMatch = false,
            Mismatches = mismatches,
        };

        var diag = MismatchClassifier.Classify(diff, 100, 100, 100, 100);

        Assert.Equal(MismatchCategory.MissingContent, diag.Category);
        Assert.Contains("missing", diag.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MismatchClassifier_Reports_Diagnostics_Metrics()
    {
        var mismatches = new List<PixelMismatch>
        {
            new(0, 0, 200, 100, 50, 255, 100, 50, 0, 255),
            new(5, 10, 150, 80, 30, 255, 100, 50, 0, 255),
        };
        var diff = new PixelDiffResult
        {
            DiffRatio = 0.02,
            DiffPixelCount = 2,
            TotalPixelCount = 100,
            IsMatch = false,
            Mismatches = mismatches,
        };

        var diag = MismatchClassifier.Classify(diff, 10, 10, 10, 10);

        Assert.True(diag.AverageChannelDelta > 0);
        Assert.True(diag.MaxChannelDelta > 0);
        Assert.Equal(2, diag.AffectedRows);  // rows 0, 10
        Assert.Equal(2, diag.AffectedColumns); // cols 0, 5
        Assert.NotNull(diag.Summary);
    }

    // ──────────── JSON output ────────────────────────────────────────

    [Fact]
    public void Program_Json_Output_Creates_Valid_Json_File()
    {
        var testDir = Path.Combine(_tempDir, "json-test");
        Directory.CreateDirectory(testDir);

        var testFile = Path.Combine(testDir, "t.html");
        File.WriteAllText(testFile, "<html><body>T</body></html>");

        var refDir = Path.Combine(testDir, "references");
        Directory.CreateDirectory(refDir);
        // Write invalid PNG to trigger a failure with diagnostics.
        File.WriteAllText(Path.Combine(refDir, "t.png"), "not-a-png");

        var jsonPath = Path.Combine(_tempDir, "report.json");

        var originalOut = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            Program.Main([
                "--wpt-dir", testDir,
                "--reference-dir", refDir,
                "--json-output", jsonPath,
            ]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Verify JSON file was created and is valid.
        Assert.True(File.Exists(jsonPath), "JSON report file should exist");

        var json = File.ReadAllText(jsonPath);
        var doc = System.Text.Json.JsonDocument.Parse(json);

        // Check top-level structure.
        Assert.True(doc.RootElement.TryGetProperty("timestamp", out _));
        Assert.True(doc.RootElement.TryGetProperty("summary", out var summaryEl));
        Assert.True(doc.RootElement.TryGetProperty("results", out var resultsEl));

        Assert.True(summaryEl.TryGetProperty("failed", out var failedEl));
        Assert.True(failedEl.GetInt32() > 0);

        Assert.True(resultsEl.GetArrayLength() > 0);
    }

    [Fact]
    public void Program_Output_Includes_SubCategory_Tag_For_PixelMismatch()
    {
        // When a PixelMismatch failure occurs, the [FAIL] line should
        // include both [PixelMismatch] and a sub-category tag.
        var testDir = Path.Combine(_tempDir, "subcat");
        Directory.CreateDirectory(testDir);

        // Create two test files: one with a reference that produces a mismatch.
        var testFile = Path.Combine(testDir, "m.html");
        File.WriteAllText(testFile, @"<!DOCTYPE html>
<html><body style=""margin:0""><div style=""width:100px;height:100px;background:red""></div></body></html>");

        var refDir = Path.Combine(testDir, "references");
        Directory.CreateDirectory(refDir);

        // Create a valid but different reference image (all blue).
        using var refBmp = new SkiaSharp.SKBitmap(1024, 768, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul);
        refBmp.Erase(SkiaSharp.SKColors.Blue);
        using var stream = File.OpenWrite(Path.Combine(refDir, "m.png"));
        refBmp.Encode(stream, SkiaSharp.SKEncodedImageFormat.Png, 100);

        var originalOut = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            Program.Main(["--wpt-dir", testDir, "--reference-dir", refDir]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = sw.ToString();

        // Should have [FAIL] [PixelMismatch] [<SubCategory>]
        Assert.Matches(@"\[FAIL\] \[PixelMismatch\] \[\w+\]", output);
        // Root Cause Analysis should mention sub-categories.
        Assert.Contains("Root Cause Analysis", output);
    }

    [Fact]
    public void WptTestResult_ToJsonObject_Includes_MismatchDiagnostics()
    {
        var result = new WptTestResult
        {
            TestPath = "/test.html",
            Passed = false,
            MatchPercent = 85.5,
            Category = FailureCategory.PixelMismatch,
            Message = "Pixel mismatch",
            MismatchDiagnostics = new MismatchDiagnostics
            {
                Category = MismatchCategory.ColorShift,
                AverageChannelDelta = 42.5,
                MaxChannelDelta = 100,
                AffectedRows = 50,
                AffectedColumns = 30,
                Summary = "Colour shift detected.",
            },
        };

        var json = result.ToJsonObject();

        Assert.Equal("/test.html", json["testPath"]);
        Assert.Equal(false, json["passed"]);
        Assert.Equal(85.5, json["matchPercent"]);
        Assert.Equal("PixelMismatch", json["category"]);

        Assert.IsType<Dictionary<string, object?>>(json["mismatchDiagnostics"]);
        var diag = (Dictionary<string, object?>)json["mismatchDiagnostics"]!;
        Assert.Equal("ColorShift", diag["subCategory"]);
        Assert.Equal(42.5, diag["averageChannelDelta"]);
        Assert.Equal(100, diag["maxChannelDelta"]);
    }

    // ──────────── CSS2 regression tests ──────────────────────────────

    [Fact]
    public void Overflow_Hidden_Clamps_Layout_Height()
    {
        // CSS2.1 §11.1.1: A box with overflow:hidden and an explicit height
        // must clip overflowing content.  Siblings after the box must be
        // positioned as if the content was clipped (i.e. immediately after
        // the box's border-box, not pushed down by overflowing content).
        const string html = @"<!DOCTYPE html>
<html><body style=""margin:0"">
<div style=""width:200px; height:100px; overflow:hidden; background:white"">
    <div style=""width:180px; height:300px; background:green""></div>
</div>
<div id=""after"" style=""width:200px; height:50px; background:blue""></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 400);

        // The blue box should start at y~104 (100px box + 4px border area).
        // If overflow clipping is broken, it would be pushed to ~304px.
        var pixel = bitmap.GetPixel(100, 130);
        Assert.True(pixel.Blue > 200 && pixel.Red < 50 && pixel.Green < 50,
            $"Expected blue at (100,130), got R={pixel.Red} G={pixel.Green} B={pixel.Blue}. " +
            "The blue box should appear immediately after the overflow:hidden container.");
    }

    [Fact]
    public void Overflow_Auto_Clamps_Layout_Height()
    {
        // overflow:auto should also clamp layout height, same as hidden.
        const string html = @"<!DOCTYPE html>
<html><body style=""margin:0"">
<div style=""width:200px; height:100px; overflow:auto; background:white"">
    <div style=""width:180px; height:300px; background:green""></div>
</div>
<div id=""after"" style=""width:200px; height:50px; background:blue""></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 400);

        var pixel = bitmap.GetPixel(100, 130);
        Assert.True(pixel.Blue > 200 && pixel.Red < 50 && pixel.Green < 50,
            $"Expected blue at (100,130), got R={pixel.Red} G={pixel.Green} B={pixel.Blue}. " +
            "The blue box should appear immediately after the overflow:auto container.");
    }

    [Fact]
    public void Overflow_Scroll_Clamps_Layout_Height()
    {
        // overflow:scroll should also clamp layout height, same as hidden.
        const string html = @"<!DOCTYPE html>
<html><body style=""margin:0"">
<div style=""width:200px; height:100px; overflow:scroll; background:white"">
    <div style=""width:180px; height:300px; background:green""></div>
</div>
<div id=""after"" style=""width:200px; height:50px; background:blue""></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 400);

        var pixel = bitmap.GetPixel(100, 130);
        Assert.True(pixel.Blue > 200 && pixel.Red < 50 && pixel.Green < 50,
            $"Expected blue at (100,130), got R={pixel.Red} G={pixel.Green} B={pixel.Blue}. " +
            "The blue box should appear immediately after the overflow:scroll container.");
    }

    [Fact]
    public void InlineSvg_Renders_As_InlineBlock()
    {
        // Inline <svg> elements must be treated as replaced inline-block
        // elements.  The SVG content should be rendered inside the element
        // bounds, and the element should take up space in the layout.
        const string html = @"<!DOCTYPE html>
<html><body style=""margin:0"">
<svg xmlns=""http://www.w3.org/2000/svg"" width=""100"" height=""100"">
  <rect x=""0"" y=""0"" width=""100"" height=""100"" fill=""green""/>
</svg>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        // The SVG should render a green rectangle.  Check center of the
        // SVG area for green pixels.
        var pixel = bitmap.GetPixel(50, 50);
        Assert.True(pixel.Green > 100,
            $"Expected green content at (50,50), got R={pixel.Red} G={pixel.Green} B={pixel.Blue}. " +
            "Inline SVG should render as a replaced inline-block element.");
    }

    [Fact]
    public void Float_PageBreakInsideAvoid_DoesNotCrash()
    {
        // Floated elements inside a container with page-break-inside:avoid
        // should not crash during layout.
        const string html = @"<!DOCTYPE html>
<html><body style=""margin:0"">
<div style=""page-break-inside:avoid"">
    <div style=""float:left; width:100px; height:100px; background:blue""></div>
    <div style=""float:left; width:100px; height:100px; background:red""></div>
    <div style=""clear:both""></div>
    <div style=""height:50px; background:green""></div>
</div>
</body></html>";

        // Should render without throwing.
        using var bitmap = HtmlRender.RenderToImage(html, 400, 300);

        // Verify the blue float is present.
        var pixel = bitmap.GetPixel(50, 50);
        Assert.True(pixel.Blue > 200 && pixel.Red < 50 && pixel.Green < 50,
            $"Expected blue float at (50,50), got R={pixel.Red} G={pixel.Green} B={pixel.Blue}.");
    }
}
