namespace Broiler.Cli.Tests;

public class AcidRenderComparisonInfrastructureTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void Acid1_Comparison_Pipeline_Files_Exist()
    {
        Assert.True(File.Exists(Path.Combine(RepoRoot, "scripts", "acid1-compare.py")));
        Assert.True(File.Exists(Path.Combine(RepoRoot, "scripts", "acid1-pixel-test.sh")));
        Assert.True(File.Exists(Path.Combine(RepoRoot, "acid", "acid1", "acid1.html")));
    }

    [Fact]
    public void Acid3_Playwright_Capture_Uses_Viewport_Screenshot()
    {
        var scriptPath = Path.Combine(RepoRoot, "scripts", "acid3-pixel-test.sh");
        Assert.True(File.Exists(scriptPath), $"Script not found at {scriptPath}");

        var script = File.ReadAllText(scriptPath);
        Assert.Contains("fullPage: false", script);
        Assert.DoesNotContain("fullPage: true", script);
    }

    [Fact]
    public void Acid_Umbrella_Roadmap_Covers_All_Three_Tests()
    {
        var roadmapPath = Path.Combine(RepoRoot, "docs", "roadmap", "acid-test-triage.md");
        Assert.True(File.Exists(roadmapPath), $"Roadmap not found at {roadmapPath}");

        var roadmap = File.ReadAllText(roadmapPath);
        Assert.Contains("## Acid1", roadmap);
        Assert.Contains("## Acid2", roadmap);
        Assert.Contains("## Acid3", roadmap);
    }
}
