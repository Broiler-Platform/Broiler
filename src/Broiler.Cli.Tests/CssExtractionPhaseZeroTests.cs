using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;
using Broiler.HTML.Core;
using Broiler.HTML.Core.Entities;
using Broiler.HTML.CSS;
using Broiler.HTML.CSS.Core.Parse;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Characterizes the CSS boundaries and the two legacy style engines before
/// their behavior is converged into Broiler.CSS.
/// </summary>
public sealed class CssExtractionPhaseZeroTests
{
    private static readonly string[] ExpectedCssProjectReferences =
    [
        @"..\..\..\Broiler.CSS\Broiler.CSS\Broiler.CSS.csproj",
        @"..\Broiler.HTML.Core\Broiler.HTML.Core.csproj",
        @"..\Broiler.HTML.Primitives\Broiler.HTML.Primitives.csproj",
        @"..\Broiler.HTML.Utils\Broiler.HTML.Utils.csproj",
    ];

    private static readonly string[] ExpectedCssFriendAssemblies =
    [
        "Broiler.Cli.Tests",
        "Broiler.HTML",
        "Broiler.HTML.CSS",
        "Broiler.HTML.Core",
        "Broiler.HTML.Dom",
        "Broiler.HTML.Image",
        "Broiler.HTML.Image.Compat",
        "Broiler.HTML.Image.Tests",
        "Broiler.HTML.Orchestration",
        "Broiler.HTML.Rendering",
        "Broiler.HTML.WPF",
    ];

    private static readonly string[] ExpectedCssDataMembers =
    [
        "Method:AddCssBlock",
        "Method:Clone",
        "Method:Combine",
        "Method:ContainsCssBlock",
        "Method:GetCssBlock",
        "Property:FontFaces",
        "Property:FontFeatureValues",
        "Property:Keyframes",
    ];

    [Fact]
    public void Existing_Css_Project_Dependencies_Are_Frozen()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(
            repositoryRoot,
            "Broiler.HTML",
            "Source",
            "Broiler.HTML.CSS",
            "Broiler.HTML.CSS.csproj");
        var project = XDocument.Load(projectPath);

        var references = project
            .Descendants("ProjectReference")
            .Select(static element => (string?)element.Attribute("Include"))
            .Where(static include => include is not null)
            .OrderBy(static include => include, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedCssProjectReferences, references);
    }

    [Fact]
    public void Existing_Css_Friend_Assembly_Surface_Is_Frozen()
    {
        var friends = typeof(CssDataParser).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(static attribute => attribute.AssemblyName.Split(',')[0])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedCssFriendAssemblies, friends);
    }

    [Fact]
    public void Existing_Public_Css_Compatibility_Surface_Is_Frozen()
    {
        Assert.Equal("Broiler.HTML.Core", typeof(CssData).Assembly.GetName().Name);
        Assert.Equal("Broiler.HTML.Core", typeof(CssBlock).Assembly.GetName().Name);
        Assert.Equal("Broiler.HTML.Core", typeof(Broiler.HTML.Core.IR.ComputedStyle).Assembly.GetName().Name);
        Assert.Equal("Broiler.HTML.CSS", typeof(CssDataParser).Assembly.GetName().Name);

        var cssDataMembers = typeof(CssData)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static member => member.MemberType is MemberTypes.Method or MemberTypes.Property)
            .Where(static member => member is not MethodInfo { IsSpecialName: true })
            .Select(static member => $"{member.MemberType}:{member.Name}")
            .OrderBy(static member => member, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedCssDataMembers, cssDataMembers);
        Assert.Equal(
            typeof(CssData),
            typeof(Broiler.HTML.Image.HtmlRender)
                .GetMethod(nameof(Broiler.HTML.Image.HtmlRender.ParseStyleSheet), [typeof(string), typeof(bool)])?
                .ReturnType);
        var repositoryRoot = FindRepositoryRoot();
        var wpfFacade = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Broiler.HTML",
            "Source",
            "Broiler.HTML.WPF",
            "HtmlRender.cs"));
        Assert.Contains(
            "public static CssData ParseStyleSheet(string stylesheet, bool combineWithDefault = true)",
            wpfFacade,
            StringComparison.Ordinal);
        Assert.NotNull(typeof(DomBridge).GetMethod(nameof(DomBridge.CalculateSpecificity), [typeof(string)]));
        Assert.NotNull(typeof(DomBridge).GetProperty(nameof(DomBridge.CssRules)));
    }

    [Fact]
    public void Renderer_Parser_Expands_Declarations_And_Preserves_Custom_Properties()
    {
        var parser = CreateRendererParser();
        var block = parser.ParseCssBlock(
            ".card",
            """
            --gap: 12px;
            margin: 1px 2px 3px 4px;
            border: 2px solid red;
            padding-left: var(--gap);
            """);

        Assert.NotNull(block);
        Assert.Equal("1px", block.Properties["margin-top"]);
        Assert.Equal("2px", block.Properties["margin-right"]);
        Assert.Equal("3px", block.Properties["margin-bottom"]);
        Assert.Equal("4px", block.Properties["margin-left"]);
        Assert.Equal("2px", block.Properties["border-top-width"]);
        Assert.Equal("solid", block.Properties["border-top-style"]);
        Assert.Equal("red", block.Properties["border-top-color"]);
        Assert.Equal("12px", block.Properties["--gap"]);
        Assert.Equal("12px", block.Properties["padding-left"]);
    }

    [Fact]
    public void Renderer_Parser_Characterizes_Colors_Lengths_And_Error_Recovery()
    {
        var parser = CreateRendererParser();
        var color = parser.ParseColor("hsl(120, 100%, 50%)");
        var block = parser.ParseCssBlock(
            "div",
            "color red; width: 25%; display: definitely-not-valid; height: calc(10px + 5px);");

        Assert.Equal(0, color.R);
        Assert.Equal(255, color.G);
        Assert.Equal(0, color.B);
        Assert.Equal(100, CssValueParser.ParseLength("25%", 400, 16));
        Assert.Equal(15, CssValueParser.ParseLength("calc(10px + 5px)", 0, 16));
        Assert.NotNull(block);
        Assert.False(block.Properties.ContainsKey("color"));
        Assert.False(block.Properties.ContainsKey("display"));
        Assert.Equal("25%", block.Properties["width"]);
        Assert.Equal("calc(10px + 5px)", block.Properties["height"]);
    }

    [Fact]
    public void Renderer_Parser_Characterizes_Media_FontFace_And_Keyframes()
    {
        const string fontFaceCss =
            "@font-face { font-family: 'Phase Zero'; src: url(phase-zero.woff2); } .sentinel { display: block; }";
        const string keyframesCss =
            "@keyframes pulse { from { opacity: 0; } 50% { opacity: .5; } to { opacity: 1; } } .sentinel { display: block; }";
        const string mediaCss =
            "@media screen { .screen-only { display: block; } } " +
            "@media print { .print-only { display: none; } } .sentinel { display: block; }";

        var fontData = CreateRendererParser().ParseStyleSheet(fontFaceCss, null);
        var keyframeData = CreateRendererParser().ParseStyleSheet(keyframesCss, null);
        var mediaData = CreateRendererParser().ParseStyleSheet(mediaCss, null);

        var face = Assert.Single(fontData.FontFaces);
        Assert.Equal("Phase Zero", face.Family);
        Assert.Contains("phase-zero.woff2", face.Src, StringComparison.Ordinal);
        Assert.Single(mediaData.GetCssBlock(".screen-only"));
        Assert.Single(mediaData.GetCssBlock(".print-only", "print"));

        Assert.True(keyframeData.Keyframes.TryGetValue("pulse", out var keyframes));
        Assert.Equal([0d, 0.5d, 1d], keyframes.Stops.Select(static stop => stop.Offset));
    }

    [Fact]
    public void Renderer_Parser_Discovers_A_Terminal_AtRule_Through_The_Shared_Parser()
    {
        const string fontFace =
            "@font-face { font-family: 'Phase Zero'; src: url(phase-zero.woff2); }";

        var terminalAtRule = CreateRendererParser().ParseStyleSheet(fontFace, null);
        var followedAtRule = CreateRendererParser().ParseStyleSheet(
            fontFace + " .sentinel { display: block; }",
            null);

        Assert.Single(terminalAtRule.FontFaces);
        Assert.Single(followedAtRule.FontFaces);
    }

    [Theory]
    [InlineData("#host > p.item[data-state='active']:first-child", 1_003_001)]
    [InlineData(":is(.card, #featured)", 1_000_000)]
    [InlineData(":where(#featured, .card)", 0)]
    [InlineData("p:nth-child(2 of #featured, .card)", 1_001_001)]
    public void Bridge_Specificity_Is_Characterized_Without_A_JavaScript_Context(
        string selector,
        int expected)
    {
        Assert.Equal(expected, DomBridge.CalculateSpecificity(selector));
    }

    [Fact]
    public void Bridge_Selector_Matcher_Is_Characterized_Without_A_JavaScript_Context()
    {
        var host = new DomElement("div", "host", null, string.Empty);
        var item = new DomElement(
            "p",
            "featured",
            "item card",
            string.Empty,
            attributes: new Dictionary<string, string> { ["data-state"] = "active" });
        var note = new DomElement("span", null, "note", string.Empty);
        var sibling = new DomElement("p", null, "item", string.Empty);
        host.Children.Add(item);
        host.Children.Add(sibling);
        item.Children.Add(note);

        Assert.True(MatchesSelector(item, "#host > p.item[data-state='active']:first-child"));
        Assert.True(MatchesSelector(item, "p:has(> span.note)"));
        Assert.True(MatchesSelector(item, "p:nth-child(1 of .item)"));
        Assert.True(MatchesSelector(item, "p:not(.missing)"));
        Assert.False(MatchesSelector(sibling, "p:first-child"));
    }

    [Fact]
    public void Differential_Corpus_Freezes_Renderer_And_Bridge_Parser_Differences()
    {
        var repositoryRoot = FindRepositoryRoot();
        var corpusPath = Path.Combine(
            repositoryRoot,
            "tests",
            "css",
            "phase0",
            "css-engine-differential-corpus.json");
        var cases = JsonSerializer.Deserialize<List<DifferentialCase>>(
            File.ReadAllText(corpusPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(cases);
        Assert.NotEmpty(cases);

        foreach (var testCase in cases)
        {
            var rendererData = CreateRendererParser().ParseStyleSheet(testCase.Css, null);
            var rendererBlock = Assert.Single(rendererData.GetCssBlock(testCase.Selector));

            using var context = new JSContext();
            var bridge = new DomBridge();
            bridge.Attach(
                context,
                $"<!doctype html><html><head><style>{testCase.Css}</style></head><body><div class=\"{testCase.ElementClass}\"></div></body></html>",
                "https://example.test/css-phase-zero");
            var bridgeRule = Assert.Single(
                bridge.CssRules,
                rule => string.Equals(rule.Selector, testCase.Selector, StringComparison.Ordinal));

            AssertProperties(testCase.Id, testCase.RendererProperties, rendererBlock.Properties);
            AssertProperties(testCase.Id, testCase.BridgeProperties, bridgeRule.Declarations);
            Assert.Equal(testCase.RendererFontFaceCount, rendererData.FontFaces.Count);
            Assert.Equal(testCase.RendererKeyframeCount, rendererData.Keyframes.Count);
            Assert.False(string.IsNullOrWhiteSpace(testCase.IntentionalDifference));
        }
    }

    private static CssParser CreateRendererParser() => new(new CharacterizationColorResolver());

    private static bool MatchesSelector(DomElement element, string selector)
    {
        var method = typeof(DomBridge).GetMethod(
            "MatchesSelector",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            [typeof(DomElement), typeof(string), typeof(DomElement)],
            modifiers: null);

        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(null, [element, selector, null]));
    }

    private static void AssertProperties(
        string caseId,
        Dictionary<string, string> expected,
        IDictionary<string, string> actual)
    {
        foreach (var (name, value) in expected)
        {
            Assert.True(actual.TryGetValue(name, out var actualValue), $"{caseId}: missing property '{name}'.");
            Assert.Equal(value, actualValue);
        }
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

    private sealed class CharacterizationColorResolver : IColorResolver
    {
        public System.Drawing.Color GetColor(string colorName) =>
            System.Drawing.Color.FromName(colorName);

        public bool IsFontExists(string family) =>
            family is "serif" or "sans-serif" or "Arial" or "Phase Zero";
    }

    private sealed class DifferentialCase
    {
        public required string Id { get; init; }
        public required string Css { get; init; }
        public required string Selector { get; init; }
        public required string ElementClass { get; init; }
        public required Dictionary<string, string> RendererProperties { get; init; }
        public required Dictionary<string, string> BridgeProperties { get; init; }
        public required int RendererFontFaceCount { get; init; }
        public required int RendererKeyframeCount { get; init; }
        public required string IntentionalDifference { get; init; }
    }
}
