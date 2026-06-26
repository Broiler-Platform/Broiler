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
                "Broiler.HTML.CSS",
                "Broiler.HTML.CSS.csproj"),
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

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(
            context,
            $"<!doctype html><html><head><style>{css}</style></head><body><div class=\"asset\"></div></body></html>",
            "https://example.test/css-phase-two");

        var assetRules = bridge.CssRules
            .Where(static rule => rule.Selector == ".asset")
            .ToArray();

        Assert.Equal(2, assetRules.Length);
        Assert.Equal(
            "url(data:image/png;base64,AAAA)",
            assetRules[0].Declarations["background-image"]);
        Assert.Equal("\"alpha;beta\"", assetRules[0].Declarations["content"]);
        Assert.Equal("alpha:beta", assetRules[0].Declarations["--Payload"]);
        Assert.Equal("calc(10px + 5px)", assetRules[1].Declarations["width"]);
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
