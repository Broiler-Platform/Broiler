using System.Xml.Linq;

namespace Broiler.UI.Tests;

public sealed class UiTopologyTests
{
    private static readonly string[] ApprovedRuntimeCategories =
    [
        "Commands",
        "Content",
        "Layout",
        "Shell",
        "Text",
        "ValueAndSelection",
    ];

    [Fact]
    public void Project_Directories_Match_Their_Roles()
    {
        string[] violations = UiProjects()
            .Select(ValidateProjectLocation)
            .Where(static violation => violation is not null)
            .Cast<string>()
            .OrderBy(static violation => violation, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Standard_Implementation_Projects_Mirror_Matching_Abstractions()
    {
        string componentRoot = FindComponentRoot();
        string[] violations = UiProjects()
            .Where(static project => project.RelativePath.StartsWith("src/Implementations/Standard/", StringComparison.Ordinal))
            .Select(project =>
            {
                string category = project.RelativePath.Split('/')[3];
                string abstractionName = project.Name[..^".Standard".Length];
                string expected = $"src/Abstractions/{category}/{abstractionName}/{abstractionName}.csproj";
                return File.Exists(Path.Combine(componentRoot, expected.Replace('/', Path.DirectorySeparatorChar)))
                    ? null
                    : $"{project.RelativePath} has no matching abstraction at {expected}";
            })
            .Where(static violation => violation is not null)
            .Cast<string>()
            .OrderBy(static violation => violation, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Abstraction_Projects_Do_Not_Reference_Standard_Implementations()
    {
        string[] violations = UiProjects()
            .Where(static project => project.RelativePath.StartsWith("src/Abstractions/", StringComparison.Ordinal))
            .SelectMany(static project => ProjectReferences(project)
                .Where(static reference => reference.ResolvedRelativePath.Contains("/src/Implementations/Standard/", StringComparison.Ordinal))
                .Select(reference => $"{project.RelativePath} references implementation {reference.RawInclude}"))
            .OrderBy(static violation => violation, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Runtime_Ui_Projects_Do_Not_Reference_Windows_Specific_Assemblies()
    {
        string[] violations = UiProjects()
            .Where(static project => project.RelativePath.StartsWith("src/", StringComparison.Ordinal))
            .SelectMany(static project => ProjectReferences(project)
                .Where(static reference =>
                    reference.RawInclude.Contains("Windows", StringComparison.Ordinal) ||
                    reference.RawInclude.Contains("Direct2D", StringComparison.Ordinal))
                .Select(reference => $"{project.RelativePath} references platform-specific project {reference.RawInclude}"))
            .OrderBy(static violation => violation, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static string? ValidateProjectLocation(ProjectInfo project)
    {
        string name = project.Name;
        string relativePath = project.RelativePath;

        if (name.EndsWith(".Tests", StringComparison.Ordinal))
            return relativePath.StartsWith("tests/", StringComparison.Ordinal)
                ? null
                : $"{relativePath} is a test project outside tests/";

        if (name.EndsWith(".Demo", StringComparison.Ordinal))
            return relativePath.StartsWith("samples/", StringComparison.Ordinal)
                ? null
                : $"{relativePath} is a demo project outside samples/";

        if (name is "Broiler.UI" or "Broiler.UI.Standard")
            return relativePath.StartsWith("src/Foundation/", StringComparison.Ordinal)
                ? null
                : $"{relativePath} is a foundation project outside src/Foundation/";

        if (name is "Broiler.UI.All")
            return relativePath.StartsWith("src/Bundles/", StringComparison.Ordinal)
                ? null
                : $"{relativePath} is a bundle project outside src/Bundles/";

        if (name is "Broiler.UI.RichEdit.Rtf")
            return relativePath.StartsWith("src/Integrations/RichEdit/", StringComparison.Ordinal)
                ? null
                : $"{relativePath} is an integration project outside src/Integrations/RichEdit/";

        if (name.EndsWith(".Standard", StringComparison.Ordinal))
            return ValidateCategorizedRuntimeProject(relativePath, "src/Implementations/Standard/", 3, "standard implementation");

        if (name.StartsWith("Broiler.UI.", StringComparison.Ordinal))
            return ValidateCategorizedRuntimeProject(relativePath, "src/Abstractions/", 2, "abstraction");

        return $"{relativePath} is not covered by the Broiler.UI topology rules";
    }

    private static string? ValidateCategorizedRuntimeProject(
        string relativePath,
        string expectedPrefix,
        int categoryIndex,
        string role)
    {
        if (!relativePath.StartsWith(expectedPrefix, StringComparison.Ordinal))
            return $"{relativePath} is a {role} project outside {expectedPrefix}";

        string[] parts = relativePath.Split('/');
        string category = parts.Length > categoryIndex ? parts[categoryIndex] : "";
        return ApprovedRuntimeCategories.Contains(category, StringComparer.Ordinal)
            ? null
            : $"{relativePath} uses unapproved {role} category '{category}'";
    }

    private static ProjectInfo[] UiProjects()
    {
        string componentRoot = FindComponentRoot();
        return Directory
            .EnumerateFiles(componentRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !IsBuildOutput(path))
            .Select(path => new ProjectInfo(
                Path.GetFileNameWithoutExtension(path),
                path,
                Path.GetRelativePath(componentRoot, path).Replace('\\', '/')))
            .OrderBy(static project => project.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static ProjectReferenceInfo[] ProjectReferences(ProjectInfo project)
    {
        string componentRoot = FindComponentRoot();
        string projectDirectory = Path.GetDirectoryName(project.FullPath)
            ?? throw new DirectoryNotFoundException($"Project directory not found for {project.FullPath}.");

        return XDocument
            .Load(project.FullPath)
            .Descendants("ProjectReference")
            .Select(reference => ((string?)reference.Attribute("Include"))?.Replace('\\', '/'))
            .Where(static include => include is not null)
            .Cast<string>()
            .Select(include =>
            {
                string resolvedPath = Path.GetFullPath(Path.Combine(projectDirectory, include.Replace('/', Path.DirectorySeparatorChar)));
                string resolvedRelativePath = Path.GetRelativePath(Path.GetDirectoryName(componentRoot)!, resolvedPath).Replace('\\', '/');
                return new ProjectReferenceInfo(include, resolvedRelativePath);
            })
            .ToArray();
    }

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(static part => part is "bin" or "obj");

    private static string FindComponentRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "Broiler.UI");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(directory.FullName, "Broiler.slnx")))
                return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Broiler.UI component root not found.");
    }

    private sealed record ProjectInfo(string Name, string FullPath, string RelativePath);

    private sealed record ProjectReferenceInfo(string RawInclude, string ResolvedRelativePath);
}
