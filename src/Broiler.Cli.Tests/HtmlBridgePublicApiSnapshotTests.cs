using System.Reflection;

namespace Broiler.Cli.Tests;

/// <summary>
/// P0.2 (HtmlBridge complexity-reduction roadmap, Phase 0): freeze the public API
/// surface of the four bridge assemblies so the later decomposition cannot silently
/// change the v2 public contract. The approved surface for each assembly is the
/// committed baseline under <c>src/Broiler.Cli.Tests/ApiBaselines/</c>.
///
/// When a change to the public surface is intentional, regenerate the baselines by
/// running the suite with the environment variable <c>UPDATE_API_BASELINES=1</c> and
/// commit the updated files alongside the change.
/// </summary>
public class HtmlBridgePublicApiSnapshotTests
{
    public static IEnumerable<object[]> BridgeAssemblies =>
    [
        ["Broiler.HtmlBridge.Core"],
        ["Broiler.HtmlBridge.Dom"],
        ["Broiler.HtmlBridge.Rendering"],
        ["Broiler.HtmlBridge.Scripting"],
    ];

    [Theory]
    [MemberData(nameof(BridgeAssemblies))]
    public void Public_Api_Surface_Matches_Committed_Baseline(string assemblyName)
    {
        var assembly = Assembly.Load(new AssemblyName(assemblyName));
        var actual = Normalize(PublicApiSurface.Describe(assembly));

        var baselineDirectory = Path.Combine(
            RepoPaths.FindRepositoryRoot(), "src", "Broiler.Cli.Tests", "ApiBaselines");
        Directory.CreateDirectory(baselineDirectory);
        var baselinePath = Path.Combine(baselineDirectory, assemblyName + ".PublicApi.txt");

        if (ShouldUpdateBaselines)
        {
            File.WriteAllText(baselinePath, actual);
            return;
        }

        Assert.True(
            File.Exists(baselinePath),
            $"Missing API baseline for {assemblyName} at {baselinePath}. " +
            "Generate it by running the suite with UPDATE_API_BASELINES=1 and commit the file.");

        var expected = Normalize(File.ReadAllText(baselinePath));
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            var receivedPath = baselinePath + ".received.txt";
            File.WriteAllText(receivedPath, actual);
            Assert.Fail(
                $"Public API surface of {assemblyName} drifted from its committed baseline.\n" +
                $"  baseline: {baselinePath}\n" +
                $"  received: {receivedPath}\n" +
                "If the change is intentional, re-run with UPDATE_API_BASELINES=1 and commit the updated baseline.");
        }
    }

    private static bool ShouldUpdateBaselines =>
        Environment.GetEnvironmentVariable("UPDATE_API_BASELINES") is "1" or "true";

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n").TrimEnd('\n');
}
