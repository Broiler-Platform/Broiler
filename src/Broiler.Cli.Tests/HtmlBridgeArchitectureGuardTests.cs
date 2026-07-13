using System.Reflection;

namespace Broiler.Cli.Tests;

/// <summary>
/// P0.2 (HtmlBridge complexity-reduction roadmap, Phase 0): lock the dependency rules
/// from the roadmap so a later extraction cannot quietly point a canonical engine at
/// the browser adapter, or the adapter at the wrong layer.
///
/// Phase 0 exit criterion: "No canonical project references a bridge or JavaScript
/// assembly." The forward-looking rules (e.g. the bridge dropping Broiler.HTML.Image)
/// land in later phases; where a rule is not yet satisfied it is pinned here as an
/// explicit baseline tripwire rather than asserted prematurely.
/// </summary>
public class HtmlBridgeArchitectureGuardTests
{
    // The canonical DOM/CSS/Layout engines. None of these may reference the browser
    // adapter or a JavaScript engine — they must stay reusable outside a browser host.
    public static IEnumerable<object[]> CanonicalAssemblies =>
    [
        ["Broiler.Dom"],
        ["Broiler.CSS"],
        ["Broiler.CSS.Dom"],
        ["Broiler.Dom.Html"],
        ["Broiler.Layout"],
    ];

    [Theory]
    [MemberData(nameof(CanonicalAssemblies))]
    public void Canonical_Assembly_Does_Not_Reference_Bridge_Or_JavaScript(string assemblyName)
    {
        var forbidden = ReferencedBroilerAssemblies(assemblyName)
            .Where(static name =>
                name.StartsWith("Broiler.HtmlBridge", StringComparison.Ordinal) ||
                name.StartsWith("Broiler.JavaScript", StringComparison.Ordinal))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(forbidden);
    }

    // The canonical kernel Broiler.Dom is dependency-free (no other Broiler assemblies).
    [Fact]
    public void Broiler_Dom_Kernel_Has_No_Broiler_Dependencies()
    {
        Assert.Empty(ReferencedBroilerAssemblies("Broiler.Dom"));
    }

    // Broiler.Dom.Html is the canonical HTML parser/serializer; it must depend only on
    // the canonical DOM kernel.
    [Fact]
    public void Broiler_Dom_Html_Depends_Only_On_Canonical_Dom()
    {
        Assert.Equal(["Broiler.Dom"], ReferencedBroilerAssemblies("Broiler.Dom.Html"));
    }

    // The bridge legitimately depends on the canonical DOM/CSS engines. This pins that
    // the canonical set is present so a future refactor cannot accidentally sever the
    // adapter from the shared engines and reintroduce a private DOM/CSS.
    [Fact]
    public void Bridge_Dom_Builds_On_The_Canonical_Dom_And_Css_Engines()
    {
        var references = ReferencedBroilerAssemblies("Broiler.HtmlBridge.Dom");

        Assert.Contains("Broiler.Dom", references);
        Assert.Contains("Broiler.CSS", references);
        Assert.Contains("Broiler.CSS.Dom", references);
    }

    // roadmap Phase 1 (project-graph repair), exit criterion:
    // "Broiler.HtmlBridge.Dom no longer references Broiler.HTML.Image." The geometry path
    // now runs through the neutral ILayoutView contract, so no project in the bridge Dom's
    // transitive project graph may reach the image-rendering stack. Walk the csproj graph
    // (not just emitted assembly metadata, which never listed Image directly) to lock it.
    [Fact]
    public void Bridge_Dom_Project_Graph_Does_Not_Reach_Html_Image()
    {
        var reachable = TransitiveProjectReferences("Broiler.HtmlBridge.Dom");
        Assert.DoesNotContain("Broiler.HTML.Image", reachable);
    }

    // The geometry provider moved out of Broiler.HtmlBridge.Rendering into
    // Broiler.HTML.Headless, so Rendering (which Dom still references for the canvas
    // recorder) must itself no longer pull the renderer.
    [Fact]
    public void Bridge_Rendering_Project_Graph_Does_Not_Reach_Html_Image()
    {
        var reachable = TransitiveProjectReferences("Broiler.HtmlBridge.Rendering");
        Assert.DoesNotContain("Broiler.HTML.Image", reachable);
    }

    // The narrow layout read-model contract the bridge depends on lives in the canonical
    // Broiler.Layout, not in a bridge or renderer assembly.
    [Fact]
    public void Layout_Owns_The_LayoutView_Contract()
    {
        Assert.Equal("Broiler.Layout", typeof(Broiler.Layout.ILayoutView).Assembly.GetName().Name);
        Assert.True(typeof(Broiler.Layout.ILayoutView).IsAssignableFrom(
            typeof(Broiler.HTML.Headless.HeadlessLayoutView)));
    }

    // Set of Broiler assembly names reachable through the csproj <ProjectReference> graph
    // rooted at the given project. Literal relative Includes are resolved; MSBuild
    // property-based Includes (e.g. $(BroilerDomPath), introduced by the Phase 1 path
    // dedup) resolve only to the canonical Dom/Graphics kernels and never toward the image
    // stack, so they are skipped rather than MSBuild-evaluated.
    private static HashSet<string> TransitiveProjectReferences(string rootProjectName)
    {
        var repositoryRoot = FindRepositoryRoot();
        var start = FindProjectFile(repositoryRoot, rootProjectName)
            ?? throw new FileNotFoundException($"Could not locate {rootProjectName}.csproj");

        var result = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(start);
        visited.Add(start);

        var includePattern = new System.Text.RegularExpressions.Regex(
            "<ProjectReference\\s+Include=\"([^\"]+)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        while (queue.Count > 0)
        {
            var projectFile = queue.Dequeue();
            var directory = Path.GetDirectoryName(projectFile)!;
            foreach (System.Text.RegularExpressions.Match match in includePattern.Matches(File.ReadAllText(projectFile)))
            {
                var include = match.Groups[1].Value;
                if (include.Contains('$', StringComparison.Ordinal))
                    continue; // property-based path (Dom/Graphics kernel) — never reaches the image stack

                var normalized = include.Replace('\\', Path.DirectorySeparatorChar);
                var fullPath = Path.GetFullPath(Path.Combine(directory, normalized));
                if (!File.Exists(fullPath) || !visited.Add(fullPath))
                    continue;

                result.Add(Path.GetFileNameWithoutExtension(fullPath));
                queue.Enqueue(fullPath);
            }
        }

        return result;
    }

    private static string? FindProjectFile(string repositoryRoot, string projectName)
    {
        var candidate = Path.Combine(repositoryRoot, "src", projectName, projectName + ".csproj");
        return File.Exists(candidate) ? candidate : null;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Broiler.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Broiler.slnx from the test output directory.");
    }

    private static string[] ReferencedBroilerAssemblies(string assemblyName) =>
        Assembly.Load(new AssemblyName(assemblyName))
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .Where(static name => name?.StartsWith("Broiler.", StringComparison.Ordinal) == true)
            .Select(static name => name!)
            .Distinct()
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
}
