using Broiler.Code.Workspaces.Model;
using Broiler.Code.Workspaces.Projects;
using Broiler.Code.Workspaces.Storage;

namespace Broiler.Code.Workspaces.Tests;

/// <summary>
/// The declared-project provider, checked against the committed Phase 0
/// fixture rather than against files written by the test. The fixture was built
/// to contain every construct in the mutation matrix, so it is the input that
/// makes these assertions mean something.
/// </summary>
public sealed class DeclaredProjectTests
{
    private static string FixtureRoot
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                string candidate = Path.Combine(
                    directory.FullName, "tests", "broiler-code-phase0", "fixture");
                if (Directory.Exists(candidate))
                    return candidate;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("The Phase 0 fixture was not found.");
        }
    }

    public static IEnumerable<object[]> FixtureProjectFiles() =>
        Directory.EnumerateFiles(FixtureRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(path => new object[] { Path.GetRelativePath(FixtureRoot, path).Replace('\\', '/') });

    /// <summary>
    /// The rule the whole provider is built around: reading and writing back
    /// without an edit changes nothing. Not "semantically equivalent" — the
    /// same characters, including comments, attribute order, blank lines, and
    /// indentation.
    /// </summary>
    [Theory]
    [MemberData(nameof(FixtureProjectFiles))]
    public void A_No_Op_Save_Is_Byte_Identical(string relativePath)
    {
        string original = File.ReadAllText(Path.Combine(FixtureRoot, relativePath));

        DeclaredProjectFile project = DeclaredProjectFile.Parse(relativePath, original);

        Assert.Equal(original, project.ToString());
    }

    [Fact(Timeout = 600000)]
    public void The_Solution_File_Also_Round_Trips_Unchanged()
    {
        string path = Path.Combine(FixtureRoot, "SampleReports.slnx");
        string original = File.ReadAllText(path);

        DeclaredSolutionFile solution = DeclaredSolutionFile.Parse("SampleReports.slnx", original);

        Assert.Equal(original, solution.ToString());
        Assert.Equal(DeclaredSolutionFile.SolutionFormat.Slnx, solution.Format);
        Assert.Equal(4, solution.Projects.Count);
        Assert.Contains(solution.Projects, p => p.Name == "SampleReports.Core");
        Assert.Contains(solution.Projects, p => p.Name == "SampleReports.Web");
    }

    [Fact(Timeout = 600000)]
    public void Classic_Sln_Project_Guids_Are_Preserved_And_Folders_Are_Not_Projects()
    {
        const string text = """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "App", "src\App\App.csproj", "{11111111-2222-3333-4444-555555555555}"
            EndProject
            Project("{2150E333-8FDC-42A3-9474-1A3956D46DE1}") = "Solution Items", "Solution Items", "{99999999-8888-7777-6666-555555555555}"
            EndProject
            Global
            EndGlobal
            """;

        DeclaredSolutionFile solution = DeclaredSolutionFile.Parse("Legacy.sln", text);

        Assert.Equal(text, solution.ToString());
        SolutionProjectEntry project = Assert.Single(solution.Projects);
        Assert.Equal("src/App/App.csproj", project.RelativePath);

        // The GUID is kept exactly. Regenerating it detaches per-user state and
        // source-control history with nothing in the file to show it happened.
        Assert.Equal("11111111-2222-3333-4444-555555555555", project.Guid);

        // The solution folder is a folder, not a project in the build graph.
        Assert.Equal("Solution Items", Assert.Single(solution.Folders));
    }

    [Fact(Timeout = 600000)]
    public void Multi_Targeting_Conditional_And_Wildcard_Constructs_Are_Classified()
    {
        string text = File.ReadAllText(
            Path.Combine(FixtureRoot, "src", "SampleReports.Core", "SampleReports.Core.csproj"));

        DeclaredProjectFile project = DeclaredProjectFile.Parse("core.csproj", text);

        Assert.Equal(["net10.0", "netstandard2.0"], project.GetTargetFrameworks());

        // A conditional ItemGroup is evaluated-only: whether its item exists
        // depends on which target framework is being built.
        Assert.Contains(project.Constructs, c =>
            c.Description.StartsWith("Conditional", StringComparison.Ordinal) &&
            c.Class == MutationClass.EvaluatedOnly);

        // `<None Include="Schemas\*.json" />` expands from the filesystem.
        Assert.Contains(project.Constructs, c =>
            c.Description == "None item" && c.Class == MutationClass.EvaluatedOnly);

        // The linked file is an ordinary editable compile item with a link.
        Assert.Contains(
            project.GetExplicitCompileItems(),
            item => item.Include.EndsWith("shared/BuildStamp.cs", StringComparison.Ordinal) &&
                item.Link == "Generated/BuildStamp.cs");
    }

    [Fact(Timeout = 600000)]
    public void Adding_A_Project_Reference_Produces_A_Minimal_Diff()
    {
        string text = File.ReadAllText(
            Path.Combine(FixtureRoot, "src", "SampleReports.App", "SampleReports.App.csproj"));
        DeclaredProjectFile project = DeclaredProjectFile.Parse("app.csproj", text);

        ProjectEditResult result = project.AddProjectReference("..\\Extra\\Extra.csproj");

        Assert.True(result.Accepted);
        string updated = result.NewText!;

        // Exactly one line added, and every original line still present in order.
        string[] before = text.Replace("\r\n", "\n").Split('\n');
        string[] after = updated.Replace("\r\n", "\n").Split('\n');
        Assert.Equal(before.Length + 1, after.Length);

        var added = after.ToList();
        foreach (string line in before)
            Assert.True(added.Remove(line), $"The edit disturbed the line: {line}");
        Assert.Contains("Extra.csproj", Assert.Single(added), StringComparison.Ordinal);

        // And it is visible to the parsed view of the new text.
        Assert.Contains(
            DeclaredProjectFile.Parse("app.csproj", updated).GetProjectReferences(),
            reference => reference.EndsWith("Extra/Extra.csproj", StringComparison.Ordinal));
    }

    [Fact(Timeout = 600000)]
    public void A_Duplicate_Project_Reference_Is_Refused()
    {
        string text = File.ReadAllText(
            Path.Combine(FixtureRoot, "src", "SampleReports.App", "SampleReports.App.csproj"));
        DeclaredProjectFile project = DeclaredProjectFile.Parse("app.csproj", text);

        ProjectEditResult result = project.AddProjectReference("..\\SampleReports.Core\\SampleReports.Core.csproj");

        Assert.False(result.Accepted);
        Assert.Equal("BRW0102", result.Code);
    }

    [Fact(Timeout = 600000)]
    public void A_Project_With_An_Unsupported_Construct_Refuses_Structural_Edits()
    {
        const string text = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <Choose>
                <When Condition="'$(X)' == 'y'">
                  <PropertyGroup><DefineConstants>Y</DefineConstants></PropertyGroup>
                </When>
              </Choose>
            </Project>
            """;

        DeclaredProjectFile project = DeclaredProjectFile.Parse("choose.csproj", text);

        Assert.True(project.HasUnsupportedConstructs);
        ProjectEditResult result = project.AddProjectReference("..\\Other\\Other.csproj");

        // Refused wholesale rather than applied around the part it understood.
        Assert.False(result.Accepted);
        Assert.Equal("BRW0101", result.Code);
        Assert.Equal(text, project.ToString());
    }

    [Fact(Timeout = 600000)]
    public void A_Custom_Target_Is_Unsupported()
    {
        const string text = """
            <Project Sdk="Microsoft.NET.Sdk">
              <Target Name="AfterBuild"><Message Text="hi" /></Target>
            </Project>
            """;

        DeclaredProjectFile project = DeclaredProjectFile.Parse("target.csproj", text);

        Assert.True(project.HasUnsupportedConstructs);
        Assert.Contains(project.Constructs, c =>
            c.Description == "<Target> element" && c.Class == MutationClass.Unsupported);
    }

    [Fact(Timeout = 600000)]
    public async Task Loading_The_Fixture_Reads_Only_What_Is_Declared()
    {
        var storage = new FileSystemWorkspaceStorage(FixtureRoot);

        StorageResult<CodeWorkspace> loaded =
            await WorkspaceLoader.LoadSolutionAsync(storage, "SampleReports.slnx");

        Assert.True(loaded.Succeeded);
        CodeWorkspace workspace = loaded.Value!;

        Assert.Equal(4, workspace.Projects.Count);
        CodeProject core = workspace.Projects.Single(p => p.Name == "SampleReports.Core");
        Assert.Equal(["net10.0", "netstandard2.0"], core.TargetFrameworks);

        CodeProject app = workspace.Projects.Single(p => p.Name == "SampleReports.App");
        Assert.Contains(app.ProjectReferences, r => r.EndsWith("SampleReports.Core.csproj", StringComparison.Ordinal));

        // The linked file resolved to its real location outside every project
        // cone, and is marked as reached through a link.
        WorkspaceItem? stamp = workspace.FindItem("shared/BuildStamp.cs");
        Assert.NotNull(stamp);
        Assert.True(stamp!.IsLinked);
    }
}
