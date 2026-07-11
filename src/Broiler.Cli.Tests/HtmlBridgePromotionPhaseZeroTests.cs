using System.Reflection;
using Broiler.HtmlBridge;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 0 ("Lock the boundary and baseline") of the HtmlBridge DOM/CSS
/// promotion roadmap
/// (<c>docs/roadmap/htmlbridge-dom-css-promotion-roadmap.md</c>).
///
/// These tests freeze the remaining allowed <c>Broiler.HtmlBridge</c>
/// compatibility seams and guard the canonical DOM/CSS components against
/// re-acquiring bridge or JavaScript-engine dependencies. They make new bridge
/// duplication visible at review time instead of allowing silent drift before
/// the promotion PR slices land.
///
/// The intended removal boundary for every seam below is
/// <c>htmlbridge-public-surface/v2</c>; see the architecture note
/// <c>docs/architecture/htmlbridge-engine-boundaries.md</c> for the seam
/// inventory, caller catalog, and ownership rationale.
/// </summary>
public sealed class HtmlBridgePromotionPhaseZeroTests
{
    /// <summary>
    /// The canonical DOM/CSS/JS assembly names. Guard tests reflect over the
    /// live assemblies so a new project reference cannot re-introduce a forbidden
    /// dependency without tripping a test.
    /// </summary>
    private const string JavaScriptEnginePrefix = "Broiler.JavaScript";

    private const string HtmlBridgePrefix = "Broiler.HtmlBridge";

    [Fact]
    public void DomElement_And_HtmlTreeBuilder_Adapter_Seam_Is_Removed_At_V2()
    {
        // htmlbridge-public-surface/v2 (RF-BRIDGE-1c Phase F4): the DomElement facade +
        // HtmlTreeBuilder materializer have reached their published removal boundary and
        // are deleted. The bridge now builds its whole tree from canonical Broiler.Dom
        // nodes via the shared HtmlDocumentParser; this guard asserts neither type can
        // silently reappear.
        var coreAssembly = typeof(Broiler.HtmlBridge.Dom.IDomBridgeRuntime).Assembly;
        var domAssembly = typeof(DomBridge).Assembly;

        Assert.Null(coreAssembly.GetType("Broiler.HtmlBridge.DomElement"));
        Assert.Null(domAssembly.GetType("Broiler.HtmlBridge.HtmlTreeBuilder"));
    }

    [Fact]
    public void CssRules_Compatibility_Tuple_View_Is_Removed_At_V2()
    {
        // htmlbridge-public-surface/v2 (Milestone 1.1): the obsolete CssRules tuple
        // view had no production callers and is removed. Consumers use the shared
        // Broiler.CSS parser (CssParser / CssStyleRule / CssDeclarationBlock)
        // directly. This guard asserts the seam is gone so it cannot silently
        // reappear.
        var cssRules = typeof(DomBridge).GetProperty(
            "CssRules",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.Null(cssRules);
    }

    [Fact]
    public void CalculateSpecificity_Static_Shim_Is_Removed_At_V2()
    {
        // htmlbridge-public-surface/v2 (Milestone 1.1): the bridge-only
        // CalculateSpecificity static delegation shim had no production callers and
        // is removed. The public replacement is
        // Broiler.CSS.CssSelectorParser.CalculateSpecificity, which stays. This
        // guard asserts the bridge shim is gone.
        var calculate = typeof(DomBridge).GetMethod(
            "CalculateSpecificity",
            BindingFlags.Public | BindingFlags.Static);

        Assert.Null(calculate);
    }

    [Fact]
    public void Bridge_Runtime_State_Remains_A_Bridge_Owned_Seam()
    {
        // ElementRuntimeState holds JavaScript identity, listeners, mutation
        // observer options, form/scroll/layout cache, and other host state. The
        // roadmap explicitly marks it a non-candidate for promotion; it stays in
        // the bridge assembly.
        var runtimeState = typeof(DomBridge).Assembly.GetType("Broiler.HtmlBridge.ElementRuntimeState");

        Assert.NotNull(runtimeState);
        Assert.Equal("Broiler.HtmlBridge.Dom", runtimeState!.Assembly.GetName().Name);
    }

    [Fact]
    public void Canonical_Dom_Does_Not_Reference_The_JavaScript_Engine()
    {
        // Phase 0 guard: Broiler.Dom must not reference JavaScript-engine
        // assemblies. The DOM kernel owns engine-neutral tree/algorithm code only.
        var javaScriptReferences = typeof(Broiler.Dom.DomDocument).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .Where(static name => name?.StartsWith(JavaScriptEnginePrefix, StringComparison.Ordinal) == true)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(javaScriptReferences);
    }

    [Fact]
    public void Canonical_Dom_Public_Surface_Does_Not_Expose_JavaScript_Types()
    {
        var leaks = typeof(Broiler.Dom.DomDocument).Assembly
            .GetExportedTypes()
            .SelectMany(static type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(GetMemberTypes)
            .Where(static type => type.Namespace?.StartsWith(JavaScriptEnginePrefix, StringComparison.Ordinal) == true)
            .Select(static type => type.FullName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(leaks);
    }

    [Fact]
    public void Canonical_CssDom_Does_Not_Reference_HtmlBridge()
    {
        // Phase 0 guard: Broiler.CSS.Dom owns selector matching, cascade, and
        // computed style over canonical DOM nodes. It must not depend on the
        // bridge, so bridge duplicates cannot leak back through a shared type.
        var bridgeReferences = typeof(Broiler.CSS.Dom.CssStyleEngine).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .Where(static name => name?.StartsWith(HtmlBridgePrefix, StringComparison.Ordinal) == true)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(bridgeReferences);
    }

    private static IEnumerable<Type> GetMemberTypes(MemberInfo member) => member switch
    {
        MethodInfo method => [method.ReturnType, .. method.GetParameters().Select(static parameter => parameter.ParameterType)],
        PropertyInfo property => [property.PropertyType],
        FieldInfo field => [field.FieldType],
        EventInfo eventInfo when eventInfo.EventHandlerType is not null => [eventInfo.EventHandlerType],
        _ => [],
    };
}
