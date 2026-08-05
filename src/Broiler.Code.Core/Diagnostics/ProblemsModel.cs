using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.UI.CodeEditor;

namespace Broiler.Code.Core.Diagnostics;

/// <summary>A row in the Problems pane.</summary>
public sealed record ProblemEntry(
    DiagnosticOrigin Origin,
    CodeDiagnosticSeverity Severity,
    string? Code,
    string Message,
    string DocumentPath,
    int Line,
    int Column,
    string? TargetFramework,
    string? BuildId)
{
    /// <summary>
    /// True when this has no source location — a project or toolchain problem.
    /// It stays in the list rather than being dropped for having nowhere to put
    /// a squiggle, which is how "the SDK is missing" becomes invisible.
    /// </summary>
    public bool IsProjectLevel => Line < 0;

    /// <summary>What a screen reader reads for the row.</summary>
    public string AccessibleDescription
    {
        get
        {
            string severity = Severity switch
            {
                CodeDiagnosticSeverity.Error => "Error",
                CodeDiagnosticSeverity.Warning => "Warning",
                CodeDiagnosticSeverity.Information => "Information",
                _ => "Hidden",
            };
            string where = IsProjectLevel
                ? DocumentPath
                : $"{DocumentPath} line {Line + 1}, column {Column + 1}";
            string origin = Origin == DiagnosticOrigin.Build ? ", from the last build" : string.Empty;
            return $"{severity} {Code}: {Message}. {where}{origin}";
        }
    }
}

public sealed record ProblemFilter(
    bool ShowErrors = true,
    bool ShowWarnings = true,
    bool ShowInformation = true,
    string? DocumentPath = null,
    string? SearchText = null)
{
    public bool Matches(ProblemEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        bool severityAllowed = entry.Severity switch
        {
            CodeDiagnosticSeverity.Error => ShowErrors,
            CodeDiagnosticSeverity.Warning => ShowWarnings,
            CodeDiagnosticSeverity.Information => ShowInformation,
            _ => false,
        };
        if (!severityAllowed)
            return false;

        if (DocumentPath is not null &&
            !string.Equals(entry.DocumentPath, DocumentPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (SearchText is { Length: > 0 } search)
        {
            // Code and message both, because users search for "CS7036" as often
            // as for a phrase from the text.
            return entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (entry.Code?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        return true;
    }
}

public sealed record ProblemCounts(int Errors, int Warnings, int Information)
{
    public int Total => Errors + Warnings + Information;

    /// <summary>
    /// The status-bar summary. It states the analysis mode, because zero errors
    /// means something different when nothing has looked for them.
    /// </summary>
    public string Describe(CodeAnalysisMode mode)
    {
        string suffix = mode switch
        {
            CodeAnalysisMode.LiveSemantic => string.Empty,
            CodeAnalysisMode.BuildDiagnostics => " (from the last build)",
            _ => " (classification only — no analysis has run)",
        };
        return $"{Errors} errors, {Warnings} warnings, {Information} information{suffix}";
    }
}

/// <summary>
/// The Problems pane's data: entries, filtering, grouping, counts, and the
/// navigation target for a row.
///
/// Counts are computed before filtering. A user who has hidden warnings still
/// needs to know there are twelve, or the filter silently becomes a claim that
/// the code is clean.
/// </summary>
public sealed class ProblemsModel
{
    private readonly List<ProblemEntry> _entries = [];

    public IReadOnlyList<ProblemEntry> Entries => _entries;

    public ProblemFilter Filter { get; set; } = new();

    public CodeAnalysisMode Mode { get; set; } = CodeAnalysisMode.ClassificationOnly;

    /// <summary>Counts over everything, ignoring the filter. See above.</summary>
    public ProblemCounts Counts => new(
        _entries.Count(e => e.Severity == CodeDiagnosticSeverity.Error),
        _entries.Count(e => e.Severity == CodeDiagnosticSeverity.Warning),
        _entries.Count(e => e.Severity == CodeDiagnosticSeverity.Information));

    public void Clear() => _entries.Clear();

    /// <summary>Replaces one document's entries, leaving other documents alone.</summary>
    public void SetDocumentEntries(string documentPath, IEnumerable<ProblemEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentNullException.ThrowIfNull(entries);

        _entries.RemoveAll(entry =>
            string.Equals(entry.DocumentPath, documentPath, StringComparison.OrdinalIgnoreCase) &&
            !entry.IsProjectLevel);
        _entries.AddRange(entries);
    }

    public void SetProjectEntries(IEnumerable<ProblemEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries.RemoveAll(entry => entry.IsProjectLevel);
        _entries.AddRange(entries);
    }

    /// <summary>
    /// The visible rows, most severe first, then by document and position — the
    /// order someone works through a list in.
    /// </summary>
    public IReadOnlyList<ProblemEntry> GetVisible() =>
    [
        .. _entries
            .Where(Filter.Matches)
            .OrderByDescending(entry => entry.Severity)
            .ThenBy(entry => entry.DocumentPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Line)
            .ThenBy(entry => entry.Column),
    ];

    /// <summary>Visible rows grouped by document, for the tree presentation.</summary>
    public IReadOnlyList<(string DocumentPath, IReadOnlyList<ProblemEntry> Entries)> GetGroupedByDocument() =>
    [
        .. GetVisible()
            .GroupBy(entry => entry.DocumentPath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => (group.Key, (IReadOnlyList<ProblemEntry>)[.. group])),
    ];

    /// <summary>
    /// Converts merged diagnostics into rows, resolving line and column against
    /// the snapshot they describe. Resolving against the current snapshot
    /// instead would place a build diagnostic at a position that has moved.
    /// </summary>
    public static IEnumerable<ProblemEntry> ToEntries(
        IEnumerable<MergedDiagnostic> diagnostics, ICodeTextSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(snapshot);

        foreach (MergedDiagnostic diagnostic in diagnostics)
        {
            int start = Math.Clamp(diagnostic.Adornment.Start, 0, snapshot.Length);
            int line = snapshot.GetLineFromPosition(start);
            yield return new ProblemEntry(
                diagnostic.Origin,
                diagnostic.Adornment.Severity,
                diagnostic.Adornment.Code,
                diagnostic.Adornment.Description,
                diagnostic.DocumentPath,
                line,
                start - snapshot.GetLineStart(line),
                diagnostic.TargetFramework,
                diagnostic.BuildId);
        }
    }

    /// <summary>A project-level row, with no position.</summary>
    public static ProblemEntry ProjectEntry(
        string projectPath, string code, string message,
        CodeDiagnosticSeverity severity = CodeDiagnosticSeverity.Error) =>
        new(DiagnosticOrigin.Workspace, severity, code, message, projectPath, -1, -1, null, null);
}
