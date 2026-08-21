using System.Reflection;
using Broiler.CSS;
using Broiler.HTML.Core;
using Broiler.HTML.Core.Entities;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;

namespace Broiler.Cli.Tests;

public sealed class CssExtractionPhaseZeroTests
{
    [Fact(Timeout = 600000)]
    public void Phase7_Legacy_Css_Project_Parser_And_Core_Models_Are_Removed()
    {
        var root = FindRepositoryRoot();
        var htmlSource = Path.Combine(root, "Broiler.HTML", "Source");

        Assert.False(File.Exists(Path.Combine(htmlSource, "Broiler.HTML.CSS", "Broiler.HTML.CSS.csproj")));

        // The directory is gone once the last shim in it is retired, which is this assertion's
        // point rather than a case it has to survive — but EnumerateFiles on a missing path throws
        // where an empty one returns nothing, so the strongest outcome would fail the test.
        var compatibilityCss = Path.Combine(htmlSource, "Broiler.HTML.Orchestration", "CompatibilityCss");
        if (Directory.Exists(compatibilityCss))
            Assert.Empty(Directory.EnumerateFiles(compatibilityCss, "*.cs", SearchOption.AllDirectories));

        var legacyModels = new[]
        {
            "CssBlock.cs",
            "CssBlockSelectorItem.cs",
            "CssFontFace.cs",
            "CssKeyframeRule.cs"
        };
        foreach (var file in legacyModels)
            Assert.False(File.Exists(Path.Combine(htmlSource, "Broiler.HTML.Core", "Entities", file)), file);

    }

    [Fact(Timeout = 600000)]
    public void Specialized_Solutions_Do_Not_Reference_The_Legacy_Css_Project()
    {
        var solutionPaths = Directory.GetFiles(
            FindRepositoryRoot(),
            "Broiler.*.slnx",
            SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(solutionPaths);
        foreach (var solutionPath in solutionPaths)
        {
            var solution = File.ReadAllText(solutionPath);
            Assert.DoesNotContain("Broiler.HTML.CSS", solution, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact(Timeout = 600000)]
    public void Phase7_CssData_Is_Only_An_Obsolete_StyleSet_Wrapper()
    {
#pragma warning disable CS0618
        var type = typeof(CssData);
#pragma warning restore CS0618
        Assert.NotNull(type.GetCustomAttribute<ObsoleteAttribute>());

        var members = type
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static member => member.MemberType is MemberTypes.Method or MemberTypes.Property)
            .Where(static member => member is not MethodInfo { IsSpecialName: true })
            .Select(static member => $"{member.MemberType}:{member.Name}")
            .OrderBy(static member => member, StringComparer.Ordinal)
            .ToArray();

        // Clone and Combine went with the compatibility window they were kept open for; what is
        // left is the wrapper this asserts CssData is, and nothing may be added back to it.
        Assert.Equal(["Property:StyleSet", "Property:StyleSheet"], members);
    }

    [Fact(Timeout = 600000)]
    public void HtmlStyleSet_Preserves_UserAgent_And_Author_Origins()
    {
        var styleSet = HtmlStyleSet.Parse(".card { color: red; }", includeDefaults: true);

        Assert.NotEmpty(styleSet.UserAgentStyleSheet.Rules);
        Assert.Single(styleSet.AuthorStyleSheet.Rules);
        Assert.True(styleSet.StyleSheet.Rules.Count > styleSet.AuthorStyleSheet.Rules.Count);
    }

    [Fact(Timeout = 600000)]
    public void Public_Facades_Expose_StyleSet_And_Shared_Model_Parsing()
    {
        const string css = ".card { color: red; margin: 1px 2px; }";

        var styleSet = HTML.Image.HtmlRender.ParseStyleSet(css, combineWithDefault: false);
        var shared = HTML.Image.HtmlRender.ParseStyleSheetModel(css, combineWithDefault: false);

        Assert.Equal(CssSerializer.Serialize(styleSet.StyleSheet), CssSerializer.Serialize(shared));
        Assert.NotNull(typeof(Broiler.HTML.Image.HtmlRender).GetMethod("ParseStyleSet", [typeof(string), typeof(bool)]));

        var renderMethods = typeof(Broiler.HTML.Image.HtmlRender)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(static method => method.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("RenderToImageWithStyleSet", renderMethods);
        Assert.Contains("RenderToImageAutoSizedWithStyleSet", renderMethods);
        Assert.Contains("RenderToImageAtAnchorWithStyleSet", renderMethods);
        Assert.Contains("RenderToFileWithStyleSet", renderMethods);
        Assert.Contains("RenderToFileAutoSizedWithStyleSet", renderMethods);

        Assert.NotNull(typeof(Broiler.HTML.Image.HtmlContainer).GetMethod("SetHtmlWithStyleSet"));
        Assert.NotNull(typeof(Broiler.HTML.Image.HtmlContainer).GetMethod("SetDocumentWithStyleSet"));
        Assert.Equal(
            typeof(CssStyleSheet),
            typeof(HtmlStylesheetLoadEventArgs).GetProperty("SetStyleSheetModel")?.PropertyType);
    }

    [Fact(Timeout = 600000)]
    public void Phase7_Layout_Friend_Surface_Contains_Only_Direct_BoxTree_Consumers()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(
            root, "Broiler.Layout", "Broiler.Layout", "Broiler.Layout.csproj"));

        var grants = project.Split('\n')
            .Where(static line => line.Contains("InternalsVisibleTo Include=", StringComparison.Ordinal))
            .Select(static line => line.Trim())
            .ToArray();

        // RF-LAYOUT-1 trimmed the friend surface to seven direct box-tree consumers; the five
        // added since each carry their reason in the csproj beside them (a nested browsing
        // context's canvas opacity, the two benchmark/runner knobs, and the two assemblies that
        // set NativeAnchorPlacement around a geometry pass). Keep this count in sync with the
        // csproj — a grant added without one is what this is here to catch.
        Assert.Equal(12, grants.Length);
        Assert.DoesNotContain(grants, static line => line.Contains("Broiler.HTML.Core", StringComparison.Ordinal));
        Assert.DoesNotContain(grants, static line => line.Contains("Broiler.HTML.Image.Compat", StringComparison.Ordinal));
        Assert.DoesNotContain(grants, static line => line.Contains("Broiler.HTML.Image.Tests", StringComparison.Ordinal));
        Assert.DoesNotContain(grants, static line => line.Contains("Broiler.HTML.WPF", StringComparison.Ordinal));
        // Broiler.HtmlBridge itself, not Broiler.HtmlBridge.Dom — the bridge assembly reaches the
        // box tree through the renderer, while .Dom is one of the two that set
        // NativeAnchorPlacement directly and is granted deliberately above.
        Assert.DoesNotContain(grants, static line => line.Contains("Broiler.HtmlBridge\"", StringComparison.Ordinal));
        Assert.DoesNotContain(grants, static line => line.Contains("Broiler.Cli\"", StringComparison.Ordinal));
    }

    [Fact(Timeout = 600000)]
    public void Phase7_Bridge_Has_No_Legacy_Cascade_Store_Or_Parser()
    {
        const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.Null(typeof(DomBridge).GetField("_cssRules", PrivateInstance));
        Assert.Null(typeof(DomBridge).GetMethod("ApplyCascadedStyles", PrivateInstance));
        Assert.Null(typeof(DomBridge).GetMethod("BuildComputedStyleMapLegacy", PrivateInstance));
        Assert.Null(typeof(DomBridge).GetMethod("EnumerateScopedStyleRules", PrivateInstance));
        Assert.Null(typeof(DomBridge).GetMethod("ParseAndApplyCssRules", PrivateInstance));
    }

    [Fact(Timeout = 600000)]
    public void Shared_Parser_Covers_Renderer_Declarations_AtRules_And_Lengths()
    {
        const string css = """
            @font-face { font-family: 'Phase Seven'; src: url(phase-seven.woff2); }
            @keyframes pulse { from { opacity: 0; } to { opacity: 1; } }
            @media screen { .card { margin: 1px 2px; } }
            """;

        var sheet = new CssParser().ParseStyleSheet(css);
        Assert.Equal(3, sheet.Rules.Count);
        Assert.Contains(sheet.Rules, static rule => rule is CssAtRule { Name: "font-face" });
        Assert.Contains(sheet.Rules, static rule => rule is CssAtRule { Name: "keyframes" });
        Assert.Equal(100, CssLengthParser.ParseLength("25%", 400, 16));
        Assert.Equal(15, CssLengthParser.ParseLength("calc(10px + 5px)", 0, 16));
        Assert.True(CssValueParser.TryParseColor("hsl(120, 100%, 50%)", out var color));
        Assert.Equal(new CssColor(0, 255, 0), color);
    }

    [Fact(Timeout = 600000)]
    public void Phase7_Renderer_Source_Has_No_Manual_Parser_Or_Selector_Cascade()
    {
        var root = FindRepositoryRoot();
        var orchestration = Path.Combine(root, "Broiler.HTML", "Source", "Broiler.HTML.Orchestration");
        var source = string.Join(
            '\n',
            Directory.EnumerateFiles(orchestration, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("class CssParser", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBlockAssignableToBoxWithSelector", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AssignCssBlock", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, ".gitmodules")) &&
                File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
