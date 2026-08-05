using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.Code.Language.CSharp.Roslyn;

/// <summary>
/// The target-specific evaluated graph a semantic service needs before it can
/// say anything true.
///
/// Roslyn alone is not a language service. A compilation with no metadata
/// references reports every framework type as missing, so the question for a
/// constrained host was never "can Roslyn run there" but "can this get there".
/// Phase 0 measured it: 168 references and 5.8 MiB for a two-project solution.
///
/// It is serializable by construction — plain strings and lists — because it
/// crosses a process boundary from a trusted worker, and may cross a network to
/// a remote one.
/// </summary>
public sealed record EvaluatedProjectGraph
{
    /// <summary>The project this graph describes, workspace-relative.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// Which target framework was evaluated. A multi-targeting project has one
    /// graph per framework and they genuinely differ — a diagnostic from the
    /// wrong one is a diagnostic about code the user is not building.
    /// </summary>
    public required string TargetFramework { get; init; }

    /// <summary>Absolute paths of the compile inputs, in evaluated order.</summary>
    public IReadOnlyList<string> CompilePaths { get; init; } = [];

    public IReadOnlyList<string> MetadataReferencePaths { get; init; } = [];

    /// <summary>
    /// Preprocessor symbols. These are what make target-specific results
    /// target-specific, so a service that guessed them would report errors in
    /// code the selected framework never compiles.
    /// </summary>
    public IReadOnlyList<string> PreprocessorSymbols { get; init; } = [];

    public string? LanguageVersion { get; init; }

    /// <summary>The project's nullable context, verbatim.</summary>
    public string? NullableContext { get; init; }

    /// <summary>
    /// Compile inputs a generator produced. Recorded separately because they
    /// exist only after a build, and a diagnostic pointing at one has to be
    /// presented as generated rather than as a file the user can edit.
    /// </summary>
    public IReadOnlyList<string> GeneratedCompilePaths { get; init; } = [];

    /// <summary>
    /// The build that produced this graph, when it came from one. Diagnostics
    /// carry it so a stale build cannot replace newer live results.
    /// </summary>
    public string? BuildId { get; init; }

    public bool HasReferences => MetadataReferencePaths.Count > 0;

    public string Describe() =>
        $"{ProjectPath} [{TargetFramework}]: {CompilePaths.Count} sources, " +
        $"{MetadataReferencePaths.Count} references, {PreprocessorSymbols.Count} defines";
}

/// <summary>
/// Why a project has no evaluated graph. The service reports one of these
/// rather than guessing: a compilation assembled from assumptions produces
/// diagnostics that look authoritative and are not.
/// </summary>
public enum GraphUnavailableReason
{
    None = 0,

    /// <summary>The workspace has not been trusted, so nothing was evaluated.</summary>
    WorkspaceNotTrusted,

    /// <summary>Evaluation ran and failed.</summary>
    EvaluationFailed,

    /// <summary>The project uses a construct the declared model cannot model.</summary>
    UnsupportedProject,

    /// <summary>No worker is reachable.</summary>
    WorkerUnavailable,
}

public sealed record GraphUnavailable(
    string ProjectPath,
    GraphUnavailableReason Reason,
    string Message)
{
    /// <summary>
    /// The user-facing diagnostic. It names the project and the reason, because
    /// "no IntelliSense" with no explanation is the failure mode this exists to
    /// avoid.
    /// </summary>
    public CodeProjectDiagnostic ToDiagnostic() => new(
        Reason switch
        {
            GraphUnavailableReason.WorkspaceNotTrusted => "BRC1000",
            GraphUnavailableReason.EvaluationFailed => "BRC1001",
            GraphUnavailableReason.UnsupportedProject => "BRC1002",
            _ => "BRC1003",
        },
        ProjectPath,
        Message);
}

/// <summary>
/// A diagnostic with no source span — a project or toolchain problem. It stays
/// visible in the Problems pane rather than being dropped for having nowhere to
/// put a squiggle.
/// </summary>
public sealed record CodeProjectDiagnostic(string Code, string ProjectPath, string Message);

/// <summary>Supplies evaluated graphs, from wherever they are produced.</summary>
public interface IEvaluatedGraphSource
{
    /// <summary>
    /// The graph for a project and target framework, or the reason there is
    /// none. Never returns a partially-guessed graph.
    /// </summary>
    bool TryGetGraph(
        string projectPath,
        string? targetFramework,
        out EvaluatedProjectGraph? graph,
        out GraphUnavailable? unavailable);

    /// <summary>Frameworks a project declares, for the selection the UI offers.</summary>
    IReadOnlyList<string> GetTargetFrameworks(string projectPath);
}

/// <summary>
/// The source used before a workspace is trusted, and the default one.
///
/// Opening a workspace does not imply trust, so this evaluates nothing and
/// reports why. An IDE that quietly evaluated on open would run the project's
/// MSBuild logic — arbitrary code — before the user agreed to anything.
/// </summary>
public sealed class UntrustedGraphSource : IEvaluatedGraphSource
{
    public bool TryGetGraph(
        string projectPath,
        string? targetFramework,
        out EvaluatedProjectGraph? graph,
        out GraphUnavailable? unavailable)
    {
        graph = null;
        unavailable = new GraphUnavailable(
            projectPath,
            GraphUnavailableReason.WorkspaceNotTrusted,
            "This workspace has not been trusted, so its projects have not been evaluated. " +
            "Editing and syntax colouring work; semantic diagnostics need an explicit trust decision.");
        return false;
    }

    public IReadOnlyList<string> GetTargetFrameworks(string projectPath) => [];
}

/// <summary>
/// A source backed by graphs a trusted worker already produced. The service
/// holds no evaluation logic of its own — evaluation runs out of process, and
/// this only remembers what came back.
/// </summary>
public sealed class CachedGraphSource : IEvaluatedGraphSource
{
    private readonly Dictionary<string, List<EvaluatedProjectGraph>> _byProject =
        new(StringComparer.OrdinalIgnoreCase);

    public void Add(EvaluatedProjectGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (!_byProject.TryGetValue(graph.ProjectPath, out List<EvaluatedProjectGraph>? graphs))
            _byProject[graph.ProjectPath] = graphs = [];

        graphs.RemoveAll(existing =>
            string.Equals(existing.TargetFramework, graph.TargetFramework, StringComparison.OrdinalIgnoreCase));
        graphs.Add(graph);
    }

    public bool TryGetGraph(
        string projectPath,
        string? targetFramework,
        out EvaluatedProjectGraph? graph,
        out GraphUnavailable? unavailable)
    {
        graph = null;
        unavailable = null;

        if (!_byProject.TryGetValue(projectPath, out List<EvaluatedProjectGraph>? graphs) || graphs.Count == 0)
        {
            unavailable = new GraphUnavailable(
                projectPath,
                GraphUnavailableReason.EvaluationFailed,
                $"'{projectPath}' has not been evaluated yet.");
            return false;
        }

        // With no framework named, the first declared one is used and the UI
        // shows which. Picking silently is what makes multi-targeting confusing.
        graph = targetFramework is null
            ? graphs[0]
            : graphs.FirstOrDefault(candidate =>
                string.Equals(candidate.TargetFramework, targetFramework, StringComparison.OrdinalIgnoreCase));

        if (graph is null)
        {
            unavailable = new GraphUnavailable(
                projectPath,
                GraphUnavailableReason.EvaluationFailed,
                $"'{projectPath}' has no evaluated graph for '{targetFramework}'.");
            return false;
        }

        return true;
    }

    public IReadOnlyList<string> GetTargetFrameworks(string projectPath) =>
        _byProject.TryGetValue(projectPath, out List<EvaluatedProjectGraph>? graphs)
            ? [.. graphs.Select(graph => graph.TargetFramework)]
            : [];
}
