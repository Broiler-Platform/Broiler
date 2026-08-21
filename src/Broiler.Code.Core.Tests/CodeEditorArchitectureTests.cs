using System.Xml.Linq;

namespace Broiler.Code.Core.Tests;

/// <summary>
/// The dependency rules ADR 0021 and the architecture document state. They are
/// asserted against the project files because that is where they are actually
/// enforceable — a comment saying "no Roslyn here" does not stop a reference
/// being added.
/// </summary>
public sealed class CodeEditorArchitectureTests
{
    [Fact(Timeout = 600000)]
    public void The_Abstraction_References_Only_Ui_And_Graphics()
    {
        string[] references = References(
            "Broiler.UI", "src", "Abstractions", "Text", "Broiler.UI.CodeEditor",
            "Broiler.UI.CodeEditor.csproj");

        Assert.Equal(
        [
            "../../../../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
            "../../../Foundation/Broiler.UI/Broiler.UI.csproj",
        ],
            references);
    }

    [Fact(Timeout = 600000)]
    public void The_Abstraction_Depends_On_No_Package_And_No_Product_Assembly()
    {
        XDocument project = Load(
            "Broiler.UI", "src", "Abstractions", "Text", "Broiler.UI.CodeEditor",
            "Broiler.UI.CodeEditor.csproj");

        Assert.Empty(project.Descendants("PackageReference"));
        Assert.DoesNotContain(References(project), reference =>
            reference.Contains("Roslyn", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("CodeAnalysis", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Broiler.Code", StringComparison.Ordinal) ||
            reference.Contains("Standard", StringComparison.Ordinal) ||
            reference.Contains("Windows", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Android", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The text layer is the base of both the editor and the later workspace
    /// model, so it must stay free of UI and of a language service. A reference
    /// added here would reach every host.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Text_Layer_Has_No_Dependencies_At_All()
    {
        XDocument project = Load("src", "Broiler.Code.Workspaces", "Broiler.Code.Workspaces.csproj");

        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("ProjectReference"));
    }

    /// <summary>
    /// Core is the only assembly that knows about both sides. That is the seam,
    /// and it must not acquire a third role: it composes control
    /// <em>abstractions</em> and the workspace, never a Standard implementation
    /// and never a platform head. A reference to either would make the shell
    /// impossible to host anywhere the implementation does not exist.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Core_Composes_Only_Abstractions_And_The_Workspace()
    {
        string[] references = References("src", "Broiler.Code.Core", "Broiler.Code.Core.csproj");

        // Listed exactly rather than by pattern: an exact list is what catches
        // a reference added without thinking about which side of the seam it
        // puts Core on.
        Assert.Equal(
        [
            "../../Broiler.Graphics/Broiler.Graphics/Broiler.Graphics.csproj",
            "../../Broiler.Input/Broiler.Input.Keyboard/Broiler.Input.Keyboard.csproj",
            "../../Broiler.Input/Broiler.Input.Mouse/Broiler.Input.Mouse.csproj",
            "../../Broiler.Input/Broiler.Input.Text/Broiler.Input.Text.csproj",
            "../../Broiler.UI/src/Abstractions/Commands/Broiler.UI.Button/Broiler.UI.Button.csproj",
            "../../Broiler.UI/src/Abstractions/Commands/Broiler.UI.Menu/Broiler.UI.Menu.csproj",
            "../../Broiler.UI/src/Abstractions/Commands/Broiler.UI.Toolbar/Broiler.UI.Toolbar.csproj",
            "../../Broiler.UI/src/Abstractions/Content/Broiler.UI.Label/Broiler.UI.Label.csproj",
            "../../Broiler.UI/src/Abstractions/Layout/Broiler.UI.Panel/Broiler.UI.Panel.csproj",
            "../../Broiler.UI/src/Abstractions/Layout/Broiler.UI.Splitter/Broiler.UI.Splitter.csproj",
            "../../Broiler.UI/src/Abstractions/Layout/Broiler.UI.TabView/Broiler.UI.TabView.csproj",
            "../../Broiler.UI/src/Abstractions/Text/Broiler.UI.CodeEditor/Broiler.UI.CodeEditor.csproj",
            "../../Broiler.UI/src/Abstractions/ValueAndSelection/Broiler.UI.TreeView/Broiler.UI.TreeView.csproj",
            "../Broiler.Code.Workspaces/Broiler.Code.Workspaces.csproj",
        ],
            references);

        Assert.DoesNotContain(references, reference =>
            reference.Contains(".Standard", StringComparison.Ordinal) ||
            reference.Contains("Windows", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Linux", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Android", StringComparison.OrdinalIgnoreCase));
    }

    private static string[] References(params string[] parts) => References(Load(parts));

    private static string[] References(XDocument project) => [.. project
        .Descendants("ProjectReference")
        .Select(reference => ((string?)reference.Attribute("Include"))?.Replace('\\', '/'))
        .Where(reference => reference is not null)
        .Cast<string>()
        .OrderBy(reference => reference, StringComparer.Ordinal)];

    private static XDocument Load(params string[] parts) => XDocument.Load(RepositoryPath(parts));

    private static string RepositoryPath(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, ".gitmodules")) &&
                File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return Path.Combine([directory.FullName, .. parts]);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
