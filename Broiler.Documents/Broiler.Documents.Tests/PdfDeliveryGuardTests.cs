using System.Text.RegularExpressions;

namespace Broiler.Documents.Tests;

/// <summary>
/// Guards the PDF delivery boundary described in the PDF support roadmap §4.1.
/// </summary>
/// <remarks>
/// The codec exists and is tested, but it is not a published capability: the
/// package is not packed, and no application composition root registers it. These
/// tests fail the build if that changes without the preview gates and the
/// clearance rows that must come with it, so "we shipped PDF by accident" is not
/// a thing that can happen quietly.
/// </remarks>
public sealed class PdfDeliveryGuardTests
{
    [Fact(Timeout = 600000)]
    public void The_Pdf_Package_Is_Not_Packable_Before_Its_Release_Gates_Pass()
    {
        string project = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Broiler.Documents/Broiler.Documents.Pdf/Broiler.Documents.Pdf.csproj"));

        Assert.Contains("<IsPackable>false</IsPackable>", project);
    }

    [Fact(Timeout = 600000)]
    public void The_Pdf_Codec_References_No_Third_Party_Runtime_Dependency()
    {
        string root = FindRepositoryRoot();
        string project = File.ReadAllText(Path.Combine(
            root,
            "Broiler.Documents/Broiler.Documents.Pdf/Broiler.Documents.Pdf.csproj"));

        // The base build is deliberately dependency-free: everything it decodes,
        // maps, or measures is implemented in this repository or in the runtime.
        Assert.DoesNotContain("<PackageReference", project);

        var allowed = new[] { "Broiler.Documents.csproj", "Broiler.Documents.Model.csproj" };
        foreach (Match match in Regex.Matches(project, @"<ProjectReference\s+Include=""([^""]+)"""))
        {
            string referenced = Path.GetFileName(match.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar));
            Assert.Contains(referenced, allowed);
        }
    }

    [Fact(Timeout = 600000)]
    public void No_Application_Composition_Root_Registers_The_Pdf_Codec()
    {
        string root = FindRepositoryRoot();
        var violations = new List<string>();

        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
                     .Where(path => !IsBuildOutput(path)))
        {
            string source = File.ReadAllText(file);
            if (source.Contains("PdfDocumentCodec", StringComparison.Ordinal) ||
                source.Contains("Broiler.Documents.Pdf", StringComparison.Ordinal))
                violations.Add(Path.GetRelativePath(root, file));
        }

        foreach (string project in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories))
        {
            if (File.ReadAllText(project).Contains("Broiler.Documents.Pdf", StringComparison.OrdinalIgnoreCase))
                violations.Add(Path.GetRelativePath(root, project));
        }

        Assert.Empty(violations);
    }

    [Fact(Timeout = 600000)]
    public void No_Pdf_Fixture_Is_Committed_Outside_The_Rights_Aware_Corpus()
    {
        string root = FindRepositoryRoot();

        // Every PDF the tests use is generated in code. A committed .pdf would need
        // an entry in the corpus manifest with its provenance and rights first.
        string[] committed = Directory
            .EnumerateFiles(Path.Combine(root, "Broiler.Documents"), "*.pdf", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(committed);
    }

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Broiler.Documents")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Broiler repository root not found.");
    }
}
