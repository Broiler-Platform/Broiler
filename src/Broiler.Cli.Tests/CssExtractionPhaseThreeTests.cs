using System.Xml.Linq;

namespace Broiler.Cli.Tests;

public sealed class CssExtractionPhaseThreeTests
{
    [Fact]
    public void Bridge_References_The_Shared_Canonical_Dom_Selector_Component()
    {
        var root = FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(
            root,
            "src",
            "Broiler.HtmlBridge.Dom",
            "Broiler.HtmlBridge.Dom.csproj"));
        var references = project
            .Descendants("ProjectReference")
            .Select(static element => (string?)element.Attribute("Include"))
            .Where(static reference => reference is not null)
            .ToArray();

        Assert.Contains(@"..\..\Broiler.CSS\Broiler.CSS.Dom\Broiler.CSS.Dom.csproj", references);
    }

    [Fact]
    public void Bridge_Selector_Surface_Is_A_Compatibility_Wrapper()
    {
        var root = FindRepositoryRoot();
        var selectorSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Broiler.HtmlBridge.Dom",
            "DomBridge.Selectors.cs"));
        var cssSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Broiler.HtmlBridge.Dom",
            "DomBridge",
            "Css.cs"));

        Assert.Contains("Broiler.CSS.Dom.CssSelectorMatcher", selectorSource, StringComparison.Ordinal);
        Assert.Contains("SharedSelectorMatcher.Matches", selectorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SplitSelectorParts", selectorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessPseudoClasses", selectorSource, StringComparison.Ordinal);
        Assert.Contains(
            "Broiler.CSS.CssSelectorParser.CalculateSpecificity(selector).Encoded",
            cssSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CalculateSpecificityComponents", cssSource, StringComparison.Ordinal);
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
        throw new DirectoryNotFoundException();
    }
}
