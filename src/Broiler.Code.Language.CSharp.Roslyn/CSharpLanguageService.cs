using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using Broiler.UI.CodeEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Broiler.Code.Language.CSharp.Roslyn;

/// <summary>One document's semantic result for one exact snapshot.</summary>
public sealed record SemanticAnalysisResult(
    ICodeTextSnapshot Snapshot,
    string DocumentPath,
    string TargetFramework,
    IReadOnlyList<CodeDiagnosticAdornment> Diagnostics,
    IReadOnlyList<CodeProjectDiagnostic> ProjectDiagnostics)
{
    public static SemanticAnalysisResult Unavailable(
        ICodeTextSnapshot snapshot, string documentPath, GraphUnavailable reason) =>
        new(snapshot, documentPath, string.Empty, [], [reason.ToDiagnostic()]);
}

/// <summary>
/// The optional C# semantic service.
///
/// Everything Roslyn is behind this boundary. A host that does not compose this
/// assembly has no Roslyn in its closure at all, which is the whole point of
/// the split — Phase 0 measured what composing it costs a browser and the
/// answer decided the composition.
///
/// The service is snapshot-exact: a result names the snapshot it was produced
/// from, and applying one to a document that has moved on is refused rather
/// than rebased.
/// </summary>
public sealed class CSharpLanguageService
{
    private readonly IEvaluatedGraphSource _graphs;
    private readonly Dictionary<string, MetadataReference> _referenceCache =
        new(StringComparer.OrdinalIgnoreCase);

    public CSharpLanguageService(IEvaluatedGraphSource graphs) =>
        _graphs = graphs ?? throw new ArgumentNullException(nameof(graphs));

    /// <summary>
    /// The compiler this service speaks, reported next to the SDK the build
    /// worker resolved. When they differ — and on the recorded baseline they do
    /// — a live diagnostic the build does not produce has a visible cause
    /// instead of looking like a bug.
    /// </summary>
    public static string CompilerIdentity { get; } =
        typeof(CSharpSyntaxTree).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(CSharpSyntaxTree).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    /// <summary>
    /// Analyses one document in the context of its project. Cross-file
    /// diagnostics fall out of this by construction: the compilation holds
    /// every source in the project, so a change to a signature in one file
    /// produces an error at its call site in another.
    /// </summary>
    public SemanticAnalysisResult Analyze(
        ICodeTextSnapshot snapshot,
        string documentPath,
        string projectPath,
        string? targetFramework,
        IReadOnlyDictionary<string, string>? dirtyOverlays,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        // Checked before anything else. A run that was superseded while queued
        // must not do work — including the graph lookup — just because its
        // first real step happens to come later.
        cancellationToken.ThrowIfCancellationRequested();

        if (!_graphs.TryGetGraph(projectPath, targetFramework, out EvaluatedProjectGraph? graph, out GraphUnavailable? unavailable))
            return SemanticAnalysisResult.Unavailable(snapshot, documentPath, unavailable!);

        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest)
            .WithPreprocessorSymbols(graph!.PreprocessorSymbols);

        var trees = new List<SyntaxTree>(graph.CompilePaths.Count);
        SyntaxTree? documentTree = null;

        foreach (string path in graph.CompilePaths.Concat(graph.GeneratedCompilePaths))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The document under the caret is read from the snapshot, not from
            // disk. Otherwise every diagnostic would describe the last saved
            // version and the user would see errors for text they already fixed.
            string text = IsSameDocument(path, documentPath)
                ? snapshot.GetText(0, snapshot.Length)
                : ReadOverlayOrFile(path, dirtyOverlays);

            SyntaxTree tree = CSharpSyntaxTree.ParseText(
                SourceText.From(text), parseOptions, path, cancellationToken: cancellationToken);
            trees.Add(tree);
            if (IsSameDocument(path, documentPath))
                documentTree = tree;
        }

        if (documentTree is null)
        {
            // The document is not in the evaluated compile set — excluded by a
            // condition, or belonging to another project. Saying so beats
            // reporting no errors, which reads as "this file is fine".
            return SemanticAnalysisResult.Unavailable(
                snapshot,
                documentPath,
                new GraphUnavailable(
                    projectPath,
                    GraphUnavailableReason.UnsupportedProject,
                    $"'{documentPath}' is not among the compile items evaluated for " +
                    $"'{projectPath}' [{graph.TargetFramework}], so it has no semantic context."));
        }

        var compilation = CSharpCompilation.Create(
            System.IO.Path.GetFileNameWithoutExtension(projectPath),
            trees,
            ResolveReferences(graph, cancellationToken),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: MapNullable(graph.NullableContext)));

        cancellationToken.ThrowIfCancellationRequested();

        // Only this document's diagnostics are adorned. The others are real but
        // belong to their own documents, and painting them here would put a
        // squiggle on the wrong file.
        SemanticModel model = compilation.GetSemanticModel(documentTree);
        ImmutableArray<Diagnostic> diagnostics = model.GetDiagnostics(cancellationToken: cancellationToken);

        var adornments = new List<CodeDiagnosticAdornment>(diagnostics.Length);
        var projectDiagnostics = new List<CodeProjectDiagnostic>();

        foreach (Diagnostic diagnostic in diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (diagnostic.Severity == DiagnosticSeverity.Hidden)
                continue;

            if (!diagnostic.Location.IsInSource)
            {
                projectDiagnostics.Add(new CodeProjectDiagnostic(
                    diagnostic.Id, projectPath, diagnostic.GetMessage()));
                continue;
            }

            TextSpan span = diagnostic.Location.SourceSpan;
            adornments.Add(new CodeDiagnosticAdornment(
                Math.Clamp(span.Start, 0, snapshot.Length),
                Math.Clamp(span.Length, 0, Math.Max(0, snapshot.Length - span.Start)),
                MapSeverity(diagnostic.Severity),
                diagnostic.GetMessage(),
                diagnostic.Id));
        }

        return new SemanticAnalysisResult(
            snapshot, documentPath, graph.TargetFramework, adornments, projectDiagnostics);
    }

    private List<MetadataReference> ResolveReferences(
        EvaluatedProjectGraph graph, CancellationToken cancellationToken)
    {
        var references = new List<MetadataReference>(graph.MetadataReferencePaths.Count);
        foreach (string path in graph.MetadataReferencePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!System.IO.File.Exists(path))
                continue;

            // Cached across analyses: reading 168 reference assemblies per
            // keystroke is what makes a naive semantic service unusable, and
            // Phase 0 measured the compile as dominated by exactly this.
            if (!_referenceCache.TryGetValue(path, out MetadataReference? reference))
                _referenceCache[path] = reference = MetadataReference.CreateFromFile(path);
            references.Add(reference);
        }

        return references;
    }

    private static string ReadOverlayOrFile(
        string path, IReadOnlyDictionary<string, string>? overlays)
    {
        // An unsaved edit in another file still has to be visible here, or a
        // cross-file diagnostic describes a version of that file the user has
        // already changed.
        if (overlays is not null && overlays.TryGetValue(path, out string? overlay))
            return overlay;

        try
        {
            return System.IO.File.ReadAllText(path);
        }
        catch (System.IO.IOException)
        {
            return string.Empty;
        }
    }

    private static bool IsSameDocument(string left, string right) =>
        string.Equals(
            System.IO.Path.GetFullPath(left),
            System.IO.Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);

    private static NullableContextOptions MapNullable(string? declared) => declared?.ToLowerInvariant() switch
    {
        "enable" => NullableContextOptions.Enable,
        "warnings" => NullableContextOptions.Warnings,
        "annotations" => NullableContextOptions.Annotations,
        _ => NullableContextOptions.Disable,
    };

    private static CodeDiagnosticSeverity MapSeverity(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => CodeDiagnosticSeverity.Error,
        DiagnosticSeverity.Warning => CodeDiagnosticSeverity.Warning,
        DiagnosticSeverity.Info => CodeDiagnosticSeverity.Information,
        _ => CodeDiagnosticSeverity.Hidden,
    };
}
