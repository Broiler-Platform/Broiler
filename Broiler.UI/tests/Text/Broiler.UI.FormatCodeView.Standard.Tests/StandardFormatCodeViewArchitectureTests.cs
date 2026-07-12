using System.Xml.Linq;

namespace Broiler.UI.FormatCodeView.Standard.Tests;

public sealed class StandardFormatCodeViewArchitectureTests
{
    [Fact]
    public void Standard_Implementation_Is_Platform_Neutral_And_Has_No_Runtime_Packages()
    {
        XDocument project = XDocument.Load(ProjectPath());
        string[] references = project.Descendants("ProjectReference")
            .Select(reference => ((string?)reference.Attribute("Include"))?.Replace('\\', '/'))
            .Where(reference => reference is not null)
            .Cast<string>()
            .ToArray();

        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Contains(references, reference => reference.Contains("Broiler.UI.FormatCodeView/", StringComparison.Ordinal));
        Assert.Contains(references, reference => reference.Contains("Broiler.UI.Standard/", StringComparison.Ordinal));
        Assert.DoesNotContain(references, reference =>
            reference.Contains("DOM", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Windows", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("WebAssembly", StringComparison.OrdinalIgnoreCase));
    }

    private static string ProjectPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "Broiler.UI",
                "src",
                "Implementations",
                "Standard",
                "Text",
                "Broiler.UI.FormatCodeView.Standard",
                "Broiler.UI.FormatCodeView.Standard.csproj");
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Standard Formatting Code view project not found.");
    }
}
