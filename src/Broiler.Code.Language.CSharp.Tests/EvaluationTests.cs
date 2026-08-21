using Broiler.Code.Language.CSharp.Roslyn;

namespace Broiler.Code.Language.CSharp.Tests;

public sealed class EvaluationTests
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

    [Fact(Timeout = 600000)]
    public async Task An_Untrusted_Workspace_Is_Never_Evaluated()
    {
        var evaluator = new DesignTimeEvaluator(FixtureRoot);

        (EvaluatedProjectGraph? graph, GraphUnavailable? unavailable) = await evaluator.EvaluateAsync(
            "src/SampleReports.App/SampleReports.App.csproj", "net10.0");

        // No process was started. Evaluating on open would run the project's
        // MSBuild logic — arbitrary code — before the user agreed to anything.
        Assert.Null(graph);
        Assert.Equal(GraphUnavailableReason.WorkspaceNotTrusted, unavailable!.Reason);
    }

    [Fact(Timeout = 600000)]
    public async Task A_Project_Outside_The_Workspace_Is_Refused()
    {
        var evaluator = new DesignTimeEvaluator(FixtureRoot) { Trust = WorkspaceTrust.Trusted };

        (EvaluatedProjectGraph? graph, GraphUnavailable? unavailable) = await evaluator.EvaluateAsync(
            "../../../../elsewhere/Escape.csproj", null);

        Assert.Null(graph);
        Assert.Equal(GraphUnavailableReason.UnsupportedProject, unavailable!.Reason);
    }

    [Fact(Timeout = 600000)]
    public async Task A_Trusted_Evaluation_Produces_The_Graph_The_Service_Needs()
    {
        var evaluator = new DesignTimeEvaluator(FixtureRoot) { Trust = WorkspaceTrust.Trusted };

        (EvaluatedProjectGraph? graph, GraphUnavailable? unavailable) = await evaluator.EvaluateAsync(
            "src/SampleReports.App/SampleReports.App.csproj", "net10.0");

        Assert.Null(unavailable);
        Assert.NotNull(graph);

        // The compile set is the evaluated one, not a directory listing:
        // ReportRunner.Corrected.cs is conditioned out and must be absent.
        Assert.Contains(graph!.CompilePaths, p => p.EndsWith("ReportRunner.Broken.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(graph.CompilePaths, p => p.EndsWith("ReportRunner.Corrected.cs", StringComparison.Ordinal));

        Assert.True(graph.HasReferences, "A graph with no references reports every framework type as missing.");
        Assert.Contains("NET10_0", graph.PreprocessorSymbols);
        Assert.Equal("net10.0", graph.TargetFramework);
    }

    [Fact(Timeout = 600000)]
    public async Task The_Evaluated_Graph_Drives_Real_Semantic_Diagnostics()
    {
        var evaluator = new DesignTimeEvaluator(FixtureRoot) { Trust = WorkspaceTrust.Trusted };
        (EvaluatedProjectGraph? graph, _) = await evaluator.EvaluateAsync(
            "src/SampleReports.App/SampleReports.App.csproj", "net10.0");

        var graphs = new CachedGraphSource();
        graphs.Add(graph!);
        var service = new CSharpLanguageService(graphs);

        string callSite = graph!.CompilePaths.Single(p => p.EndsWith("ReportRunner.Broken.cs", StringComparison.Ordinal));
        var buffer = new Broiler.Code.Workspaces.Text.SourceBuffer(await File.ReadAllTextAsync(callSite));
        using var document = new Broiler.Code.Core.SourceBufferDocument(buffer);

        SemanticAnalysisResult result = service.Analyze(
            document.Snapshot, callSite, graph.ProjectPath, "net10.0", null, CancellationToken.None);

        // End to end: a real evaluation feeding a real compilation, producing
        // the same error the authoritative build reports at the same place.
        Assert.Contains(result.Diagnostics, d => d.Code == "CS7036");
    }

    [Fact(Timeout = 600000)]
    public void Generated_Compile_Inputs_Are_Separated_From_The_Users_Files()
    {
        // Backslashes escaped, as MSBuild emits them on Windows. A worker on
        // another platform sends forward slashes, and both must classify the
        // same way.
        const string json = """
            {
              "Items": {
                "Compile": [
                  { "FullPath": "C:\\p\\Program.cs" },
                  { "FullPath": "C:\\p\\obj\\Debug\\Generated.g.cs" },
                  { "FullPath": "/home/p/obj/Debug/Other.g.cs" }
                ],
                "ReferencePath": [ { "FullPath": "C:\\ref\\System.Runtime.dll" } ]
              },
              "Properties": {
                "DefineConstants": "TRACE;NET10_0",
                "TargetFramework": "net10.0",
                "Nullable": "enable"
              }
            }
            """;

        EvaluatedProjectGraph graph = DesignTimeEvaluator.Parse("p.csproj", null, json);

        // A generator's output is a build product. A diagnostic on one must be
        // shown as generated rather than as a file the user can edit.
        Assert.Equal([@"C:\p\Program.cs"], graph.CompilePaths);
        Assert.Equal(2, graph.GeneratedCompilePaths.Count);

        Assert.Equal(["TRACE", "NET10_0"], graph.PreprocessorSymbols);
        Assert.Equal("enable", graph.NullableContext);
    }

    [Fact(Timeout = 600000)]
    public void A_Cached_Source_Keeps_One_Graph_Per_Target_Framework()
    {
        var graphs = new CachedGraphSource();
        graphs.Add(new EvaluatedProjectGraph { ProjectPath = "p.csproj", TargetFramework = "net10.0" });
        graphs.Add(new EvaluatedProjectGraph { ProjectPath = "p.csproj", TargetFramework = "netstandard2.0" });

        Assert.Equal(["net10.0", "netstandard2.0"], graphs.GetTargetFrameworks("p.csproj"));

        Assert.True(graphs.TryGetGraph("p.csproj", "netstandard2.0", out EvaluatedProjectGraph? chosen, out _));
        Assert.Equal("netstandard2.0", chosen!.TargetFramework);

        // An unknown framework is refused rather than silently served another.
        Assert.False(graphs.TryGetGraph("p.csproj", "net48", out _, out GraphUnavailable? missing));
        Assert.Equal(GraphUnavailableReason.EvaluationFailed, missing!.Reason);
    }
}
