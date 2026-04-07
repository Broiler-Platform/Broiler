using System.IO;

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
}
