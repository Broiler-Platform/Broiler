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

    // roadmap Phase 1 target: replace the Dom -> Rendering -> Broiler.HTML.Image path
    // with a narrow ILayoutView contract. Until then the dependency exists; this
    // tripwire pins the current reality so its removal in Phase 1 is a deliberate,
    // reviewed change (update or delete this test when the reference is gone).
    [Fact]
    public void Baseline_Bridge_Rendering_Still_References_Html_Image_Until_Phase1()
    {
        var references = ReferencedBroilerAssemblies("Broiler.HtmlBridge.Rendering");
        Assert.Contains("Broiler.HTML.Image", references);
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
