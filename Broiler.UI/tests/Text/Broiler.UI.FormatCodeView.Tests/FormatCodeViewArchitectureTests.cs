using System.Xml.Linq;

namespace Broiler.UI.FormatCodeView.Tests;

public sealed class FormatCodeViewArchitectureTests
{
    [Fact]
    public void Abstraction_References_Only_Projector_Ui_And_Graphics()
    {
        XDocument project = XDocument.Load(ProjectPath(
            "src", "Abstractions", "Text", "Broiler.UI.FormatCodeView", "Broiler.UI.FormatCodeView.csproj"));
        string[] references = References(project);

        Assert.Equal(
        [
            "../../../../../Broiler.Documents/Broiler.Documents.FormatCodes/Broiler.Documents.FormatCodes.csproj",
            "../../../../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
            "../../../Foundation/Broiler.UI/Broiler.UI.csproj",
        ],
            references);
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.DoesNotContain(references, reference =>
            reference.Contains("Standard", StringComparison.Ordinal) ||
            reference.Contains("DOM", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Windows", StringComparison.OrdinalIgnoreCase));
    }

    private static string[] References(XDocument project) => project
        .Descendants("ProjectReference")
        .Select(reference => ((string?)reference.Attribute("Include"))?.Replace('\\', '/'))
        .Where(reference => reference is not null)
        .Cast<string>()
        .OrderBy(reference => reference, StringComparer.Ordinal)
        .ToArray();

    internal static string ProjectPath(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string root = Path.Combine(directory.FullName, "Broiler.UI");
            if (Directory.Exists(root) && File.Exists(Path.Combine(directory.FullName, "Broiler.slnx")))
                return Path.Combine([root, .. parts]);
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Broiler.UI root not found.");
    }
}
