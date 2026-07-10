using System.Xml.Linq;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

public sealed class CssExtractionPhaseTwoTests
{
    [Fact]
    public void Renderer_And_Bridge_Reference_The_Shared_Css_Kernel()
    {
        var root = FindRepositoryRoot();

        AssertProjectReferences(
            Path.Combine(
                root,
                "Broiler.HTML",
                "Source",
                "Broiler.HTML.Orchestration",
                "Broiler.HTML.Orchestration.csproj"),
            @"..\..\..\Broiler.CSS\Broiler.CSS\Broiler.CSS.csproj");
        AssertProjectReferences(
            Path.Combine(
                root,
                "src",
                "Broiler.HtmlBridge.Dom",
                "Broiler.HtmlBridge.Dom.csproj"),
            @"..\..\Broiler.CSS\Broiler.CSS\Broiler.CSS.csproj");
    }

    [Fact]
    public void Bridge_Uses_Shared_Parsing_For_Complex_Declarations_And_Media_Rules()
    {
        const string css = """
            .asset {
              background-image: url(data:image/png;base64,AAAA);
              content: "alpha;beta";
              --Payload: alpha:beta;
            }
            @media screen {
              .asset { width: calc(10px + 5px); }
            }
            """;

        // Was a characterization test over the obsolete DomBridge.CssRules tuple
        // view; rerouted to the shared Broiler.CSS parser (the single source of
        // truth) when that seam was removed at htmlbridge-public-surface/v2. The
        // shared parser must keep complex declaration values intact — a data-URI
        // with commas, a string with a semicolon, a custom property with a colon,
        // and a calc() inside an @media block.
        var sheet = new Broiler.CSS.CssParser().ParseStyleSheet(css);

        var baseAsset = sheet.Rules.OfType<Broiler.CSS.CssStyleRule>()
            .Single(static r => r.Selectors.Selectors.Any(static s => s.Text.Trim() == ".asset"));
        Assert.Equal("url(data:image/png;base64,AAAA)", baseAsset.Declarations.GetPropertyValue("background-image"));
        Assert.Equal("\"alpha;beta\"", baseAsset.Declarations.GetPropertyValue("content"));
        Assert.Equal("alpha:beta", baseAsset.Declarations.GetPropertyValue("--Payload"));

        var mediaRule = sheet.Rules.OfType<Broiler.CSS.CssAtRule>()
            .Single(static r => r.Name.Equals("media", System.StringComparison.OrdinalIgnoreCase));
        var mediaAsset = mediaRule.Rules.OfType<Broiler.CSS.CssStyleRule>()
            .Single(static r => r.Selectors.Selectors.Any(static s => s.Text.Trim() == ".asset"));
        Assert.Equal("calc(10px + 5px)", mediaAsset.Declarations.GetPropertyValue("width"));
    }

    [Fact]
    public void Bridge_No_Longer_Declares_Regex_Rule_Splitters()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Broiler.HtmlBridge.Dom",
            "DomBridge",
            "Css.cs"));
        var animationSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Broiler.HtmlBridge.Dom",
            "DomBridge",
            "AnimationResolver.cs"));

        Assert.DoesNotContain("CssRulePattern", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MediaQueryPattern", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BlockAtRulePattern", source, StringComparison.Ordinal);
        Assert.DoesNotContain("KeyframesRulePattern", animationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AnimCssRulePattern", animationSource, StringComparison.Ordinal);
    }

    private static void AssertProjectReferences(string projectPath, string expectedReference)
    {
        var references = XDocument.Load(projectPath)
            .Descendants("ProjectReference")
            .Select(static element => (string?)element.Attribute("Include"))
            .Where(static include => include is not null)
            .ToArray();

        Assert.Contains(expectedReference, references);
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

        throw new DirectoryNotFoundException("Could not locate Broiler.slnx.");
    }
}
