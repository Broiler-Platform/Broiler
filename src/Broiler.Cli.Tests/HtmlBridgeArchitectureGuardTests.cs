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

    // roadmap Phase 1 (project-graph repair), exit criterion: "One Broiler.Dom assembly node
    // and one Graphics implementation in a solution build." Submodules carry nested checkouts
    // of both kernels for standalone builds; in-tree every consumer resolves the single ROOT
    // checkout via $(BroilerDomPath)/$(BroilerGraphicsPath), so the main solution lists exactly
    // one node for each canonical kernel csproj.
    [Theory]
    [InlineData("Broiler.Dom.csproj")]
    [InlineData("Broiler.Graphics.csproj")]
    public void Solution_Builds_A_Single_Canonical_Kernel_Node(string projectFileName)
    {
        var solutionPath = Path.Combine(FindRepositoryRoot(), "Broiler.slnx");
        var pattern = new System.Text.RegularExpressions.Regex(
            "Path=\"[^\"]*/" + System.Text.RegularExpressions.Regex.Escape(projectFileName) + "\"");
        var count = pattern.Matches(File.ReadAllText(solutionPath)).Count;

        Assert.Equal(1, count);
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

    // Roadmap Phase 3 exit criterion: "No production source file exceeds 750 lines without a
    // documented exemption." This guard enforces that for the HtmlBridge assemblies (the god-object
    // focus of Phase 3): a NEW oversized file fails the test, forcing the work into a smaller module
    // (the P3.x feature-module pattern) rather than another giant partial. The exemptions below are
    // the pre-existing debt the decomposition is chipping away at — each is a file to SHRINK, not a
    // ceiling to grow into; when one drops to 750 lines its entry should be removed. Raising the
    // limit or adding an exemption is a deliberate, reviewed act, not the default.
    private const int MaxProductionFileLines = 750;

    // Documented Phase-3 debt: files that still exceed the limit (line counts as of 2026-07-16).
    private static readonly HashSet<string> OversizedFileExemptions = new(StringComparer.Ordinal)
    {
        "src/Broiler.HtmlBridge.Dom/DomBridge/LayoutMetrics.cs",
        // JsFunctionCallbacks/JsObjects.cs de-listed 2026-07-17: six feature modules
        // (P3.40–P3.45: CharacterData, node accessors, element attributes, node relationships,
        // Element selectors, element traversal) plus the EventTarget slice (P3.46: addEventListener/
        // removeEventListener/dispatchEvent/click/focus/blur) dropped it from 1599 to 727.
        "src/Broiler.HtmlBridge.Dom/DomBridge/JsObjects.cs",
        // JsFunctionCallbacks/Registration.cs de-listed 2026-07-17: nine feature modules
        // (P3.19–P3.27: console, crypto, sendBeacon, matchMedia, timers, write/writeln, node
        // factories, element queries, live collections, node mutation) dropped it from 1184 to 684.
        // SubDocuments.cs de-listed 2026-07-17: two cohesive clusters were split out — the generic
        // HTML-fragment DOM-mutation helpers (RemoveElementsRecursive, NormalizeNode, RemoveChildAt,
        // insertAdjacent/innerHTML/outerHTML builders) into DomBridge/HtmlFragmentMutation.cs, and
        // XML/XHTML sub-document construction plus sub-document script execution
        // (BuildSubDocumentFromXml, BuildDomElementFromXElement, ExecuteSubDocumentScripts) into
        // DomBridge/SubDocuments.XmlAndScripts.cs — dropping it from 1152 to 694 lines.
        // DomBridge.cs de-listed 2026-07-17: two cohesive behaviour clusters were split out of the
        // facade into sibling partials — the window-load lifecycle / window-event dispatch
        // (FireWindowLoadEvent, DispatchWindowEvent, BuildWindowFramesArray/CollectWindowFrames) into
        // DomBridge.WindowLoad.cs, and initial HTML/doctype/inline-style parsing (ParseHtml,
        // ParseStyle, IsAcceptableInlineValue, DocTypePattern) into DomBridge.HtmlParsing.cs —
        // dropping the facade from 1013 to 682 lines (within the 500-800 facade target).
        // DomBridge.Serialization.cs de-listed 2026-07-17: its cohesive SVG zoom-serialization
        // attribute-scaling cluster (ApplyZoomSerializationSvgAttributes and the ScaleSvg* / SVG
        // font-relative-unit resolution helpers, the SVG unit sets, and the three [GeneratedRegex]
        // point/path/font-shorthand patterns) was split into the sibling partial
        // DomBridge.Serialization.SvgZoom.cs, dropping it from 951 to 668 lines.
        // Utilities.cs de-listed 2026-07-17: its cohesive DOM name-validation / DOMException /
        // JS-constructor-globals cluster (ThrowDOMException, ValidateElementName/QualifiedName,
        // RegisterDOMException/Node/SVGLength, and the two XML-name regex patterns) was split into
        // the sibling partial Utilities.NameValidation.cs, dropping it from 894 to 626 lines.
        // AnimationResolver.cs de-listed 2026-07-17: its CSS timing-function/easing cluster was
        // split into the sibling partial AnimationResolver.Timing.cs, dropping it to 644 lines.
    };

    [Fact]
    public void No_New_HtmlBridge_Production_File_Exceeds_The_Line_Limit()
    {
        var root = FindRepositoryRoot();
        string[] bridgeDirs =
        [
            "src/Broiler.HtmlBridge.Core", "src/Broiler.HtmlBridge.Dom",
            "src/Broiler.HtmlBridge.Rendering", "src/Broiler.HtmlBridge.Scripting",
        ];

        var offenders = new List<string>();
        var staleExemptions = new List<string>();

        foreach (var dir in bridgeDirs)
        {
            var full = Path.Combine(root, dir);
            if (!Directory.Exists(full))
                continue;

            foreach (var file in Directory.EnumerateFiles(full, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    continue;

                var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                var lines = File.ReadAllLines(file).Length;
                bool exempt = OversizedFileExemptions.Contains(rel);

                if (lines > MaxProductionFileLines && !exempt)
                    offenders.Add($"{rel} ({lines} lines)");
                else if (lines <= MaxProductionFileLines && exempt)
                    staleExemptions.Add(rel);
            }
        }

        Assert.True(offenders.Count == 0,
            $"New/grown HtmlBridge source files exceed {MaxProductionFileLines} lines (roadmap Phase 3: split "
            + "into a feature module rather than growing the god object):\n  " + string.Join("\n  ", offenders));

        // A file that has been reduced under the limit should be taken off the debt list so the ratchet
        // keeps closing — surface it rather than silently allow it to grow back.
        Assert.True(staleExemptions.Count == 0,
            "These files no longer exceed the limit; remove them from OversizedFileExemptions:\n  "
            + string.Join("\n  ", staleExemptions));
    }
}
