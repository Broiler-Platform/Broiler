using System.Xml.Linq;

namespace Broiler.Cli.Tests;

public sealed class CssExtractionPhaseThreeTests
{
    [Fact(Timeout = 600000)]
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
        throw new DirectoryNotFoundException();
    }
}
