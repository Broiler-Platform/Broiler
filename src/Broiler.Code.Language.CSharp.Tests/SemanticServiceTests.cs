using Broiler.Code.Core;
using Broiler.Code.Language.CSharp.Roslyn;
using Broiler.Code.Workspaces.Text;
using Broiler.UI.CodeEditor;

namespace Broiler.Code.Language.CSharp.Tests;

/// <summary>
/// The semantic service against the committed Phase 0 fixture — the same
/// solution the build worker compiles, so "the live service agrees with the
/// build" is a claim about one artefact rather than two similar ones.
/// </summary>
public sealed class SemanticServiceTests
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

    private const string AppProject = "src/SampleReports.App/SampleReports.App.csproj";

    private static string AppPath(params string[] parts) =>
        Path.Combine([FixtureRoot, "src", "SampleReports.App", .. parts]);

    private static string CorePath(params string[] parts) =>
        Path.Combine([FixtureRoot, "src", "SampleReports.Core", .. parts]);

    /// <summary>
    /// A graph assembled the way a trusted worker would deliver one: the
    /// evaluated compile set plus the framework references. Reference paths
    /// come from the running SDK's reference pack, which is what the real
    /// evaluation resolves too.
    /// </summary>
    private static EvaluatedProjectGraph BuildAppGraph(bool includeCore = true)
    {
        var compiles = new List<string>
        {
            AppPath("Program.cs"),
            AppPath("Reporting", "ReportRunner.Broken.cs"),
            AppPath("Reporting", "LegacySummary.cs"),
        };

        if (includeCore)
        {
            compiles.Add(CorePath("Reporting", "Report.cs"));
            compiles.Add(CorePath("Reporting", "FormatOptions.cs"));
            compiles.Add(CorePath("Reporting", "ReportFormatter.cs"));
            compiles.Add(Path.Combine(FixtureRoot, "shared", "BuildStamp.cs"));
        }

        return new EvaluatedProjectGraph
        {
            ProjectPath = AppProject,
            TargetFramework = "net10.0",
            CompilePaths = compiles,
            MetadataReferencePaths = FrameworkReferences(),
            PreprocessorSymbols = ["NET", "NET10_0", "NET10_0_OR_GREATER"],
            NullableContext = "enable",
        };
    }

    private static IReadOnlyList<string> FrameworkReferences()
    {
        string trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        return [.. trusted.Split(Path.PathSeparator).Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))];
    }

    private static (SourceBufferDocument Document, SourceBuffer Buffer) Open(string path)
    {
        var buffer = new SourceBuffer(File.ReadAllText(path));
        return (new SourceBufferDocument(buffer), buffer);
    }

    [Fact(Timeout = 600000)]
    public void The_Compiler_Identity_Is_Reported_So_A_Mismatch_Is_Visible()
    {
        // The service and the build worker run different compiler builds on the
        // recorded baseline. Reporting it is what makes a live diagnostic the
        // build does not produce explicable rather than mysterious.
        Assert.False(string.IsNullOrWhiteSpace(CSharpLanguageService.CompilerIdentity));
        Assert.StartsWith("5.", CSharpLanguageService.CompilerIdentity, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Cross_File_Error_Is_Reported_At_Its_Call_Site()
    {
        var graphs = new CachedGraphSource();
        graphs.Add(BuildAppGraph());
        var service = new CSharpLanguageService(graphs);
        (SourceBufferDocument document, _) = Open(AppPath("Reporting", "ReportRunner.Broken.cs"));

        SemanticAnalysisResult result = service.Analyze(
            document.Snapshot,
            AppPath("Reporting", "ReportRunner.Broken.cs"),
            AppProject,
            "net10.0",
            null,
            CancellationToken.None);

        // The symbol is declared in another file; the error belongs here.
        CodeDiagnosticAdornment error = Assert.Single(
            result.Diagnostics.Where(d => d.Severity == CodeDiagnosticSeverity.Error));
        Assert.Equal("CS7036", error.Code);

        (int line, _) = LineAndColumn(document.Snapshot, error.Start);
        Assert.Equal(17, line);
        document.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Editing_One_File_Changes_The_Diagnostic_In_Another()
    {
        var graphs = new CachedGraphSource();
        graphs.Add(BuildAppGraph());
        var service = new CSharpLanguageService(graphs);

        string callSite = AppPath("Reporting", "ReportRunner.Broken.cs");
        string declaration = CorePath("Reporting", "ReportFormatter.cs");
        (SourceBufferDocument document, _) = Open(callSite);

        Assert.Contains(
            service.Analyze(document.Snapshot, callSite, AppProject, "net10.0", null, CancellationToken.None).Diagnostics,
            d => d.Code == "CS7036");

        // The fix is made in the *other* file, as an unsaved overlay: give the
        // second parameter a default and the call site becomes valid.
        string patched = File.ReadAllText(declaration)
            .Replace(
                "public static string Format(Report report, FormatOptions options)",
                "public static string Format(Report report, FormatOptions? options = null)",
                StringComparison.Ordinal)
            .Replace("if (options is null)", "if (options is null) options = FormatOptions.Invariant;\n        if (false)", StringComparison.Ordinal);

        SemanticAnalysisResult after = service.Analyze(
            document.Snapshot,
            callSite,
            AppProject,
            "net10.0",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [declaration] = patched },
            CancellationToken.None);

        // No edit was made to the file under the caret, and its error is gone.
        Assert.DoesNotContain(after.Diagnostics, d => d.Code == "CS7036");
        document.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Target_Specific_Defines_Produce_Target_Specific_Results()
    {
        string source = """
            public static class Probe
            {
            #if LEGACY_TARGET
                public static int Value => Missing.Symbol;
            #else
                public static int Value => 1;
            #endif
            }
            """;
        string path = Path.Combine(Path.GetTempPath(), $"probe-{Guid.NewGuid():n}.cs");
        File.WriteAllText(path, source);

        try
        {
            var buffer = new SourceBuffer(source);
            using var document = new SourceBufferDocument(buffer);

            SemanticAnalysisResult withoutDefine = Analyze(path, document, []);
            SemanticAnalysisResult withDefine = Analyze(path, document, ["LEGACY_TARGET"]);

            // The same text, two frameworks, two answers. A service that
            // guessed the defines would report one of these for both.
            Assert.Empty(withoutDefine.Diagnostics.Where(d => d.Severity == CodeDiagnosticSeverity.Error));
            Assert.NotEmpty(withDefine.Diagnostics.Where(d => d.Severity == CodeDiagnosticSeverity.Error));
        }
        finally
        {
            File.Delete(path);
        }

        static SemanticAnalysisResult Analyze(
            string path, SourceBufferDocument document, string[] defines)
        {
            var graphs = new CachedGraphSource();
            graphs.Add(new EvaluatedProjectGraph
            {
                ProjectPath = "p.csproj",
                TargetFramework = defines.Length == 0 ? "net10.0" : "netstandard2.0",
                CompilePaths = [path],
                MetadataReferencePaths = FrameworkReferences(),
                PreprocessorSymbols = defines,
            });

            return new CSharpLanguageService(graphs).Analyze(
                document.Snapshot, path, "p.csproj",
                defines.Length == 0 ? "net10.0" : "netstandard2.0", null, CancellationToken.None);
        }
    }

    [Fact(Timeout = 600000)]
    public void An_Untrusted_Workspace_Reports_Why_Rather_Than_Guessing()
    {
        var service = new CSharpLanguageService(new UntrustedGraphSource());
        var buffer = new SourceBuffer("class C { }\n");
        using var document = new SourceBufferDocument(buffer);

        SemanticAnalysisResult result = service.Analyze(
            document.Snapshot, "C.cs", "p.csproj", null, null, CancellationToken.None);

        // No diagnostics and no silence: the reason is a project-level entry.
        Assert.Empty(result.Diagnostics);
        CodeProjectDiagnostic reason = Assert.Single(result.ProjectDiagnostics);
        Assert.Equal("BRC1000", reason.Code);
        Assert.Contains("not been trusted", reason.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Document_Outside_The_Evaluated_Compile_Set_Says_So()
    {
        var graphs = new CachedGraphSource();
        graphs.Add(BuildAppGraph());
        var service = new CSharpLanguageService(graphs);

        // ReportRunner.Corrected.cs is conditioned out of the project, so it has
        // no semantic context. Reporting no errors would read as "this is fine".
        var buffer = new SourceBuffer(File.ReadAllText(AppPath("Reporting", "ReportRunner.Corrected.cs")));
        using var document = new SourceBufferDocument(buffer);

        SemanticAnalysisResult result = service.Analyze(
            document.Snapshot,
            AppPath("Reporting", "ReportRunner.Corrected.cs"),
            AppProject,
            "net10.0",
            null,
            CancellationToken.None);

        Assert.Equal("BRC1002", Assert.Single(result.ProjectDiagnostics).Code);
    }

    [Fact(Timeout = 600000)]
    public void Analysis_Observes_Cancellation()
    {
        var graphs = new CachedGraphSource();
        graphs.Add(BuildAppGraph());
        var service = new CSharpLanguageService(graphs);
        (SourceBufferDocument document, _) = Open(AppPath("Program.cs"));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => service.Analyze(
            document.Snapshot, AppPath("Program.cs"), AppProject, "net10.0", null, cancellation.Token));

        document.Dispose();
    }

    private static (int Line, int Column) LineAndColumn(ICodeTextSnapshot snapshot, int position)
    {
        int line = snapshot.GetLineFromPosition(position);
        return (line, position - snapshot.GetLineStart(line));
    }
}
