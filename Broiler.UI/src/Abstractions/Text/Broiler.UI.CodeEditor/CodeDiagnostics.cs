using System;
using System.Collections.Generic;

namespace Broiler.UI.CodeEditor;

public enum CodeDiagnosticSeverity
{
    Hidden = 0,
    Information,
    Warning,
    Error,
}

/// <summary>
/// One diagnostic as the control sees it: a span, a severity, and an accessible
/// description.
///
/// The control never parses <paramref name="Description"/> and never derives
/// behavior from it. Identity belongs to the producer's diagnostic model, where
/// it is the rule code, document, and span — never the message, which is
/// localized.
/// </summary>
public sealed record CodeDiagnosticAdornment(
    int Start,
    int Length,
    CodeDiagnosticSeverity Severity,
    string Description,
    string? Code = null)
{
    public int End => Start + Length;
}

/// <summary>
/// Diagnostics for one exact snapshot, indexed by line so the renderer can ask
/// for the visible range without scanning the whole set.
/// </summary>
public sealed class CodeDiagnosticSet
{
    private static readonly CodeDiagnosticAdornment[] None = [];

    private readonly Dictionary<int, CodeDiagnosticAdornment[]> _byLine;

    private CodeDiagnosticSet(
        ICodeTextSnapshot snapshot,
        Dictionary<int, CodeDiagnosticAdornment[]> byLine,
        int count)
    {
        Snapshot = snapshot;
        _byLine = byLine;
        Count = count;
    }

    public ICodeTextSnapshot Snapshot { get; }

    public int Count { get; }

    public static CodeDiagnosticSet Empty(ICodeTextSnapshot snapshot) =>
        new(snapshot, [], 0);

    public static CodeDiagnosticSet Create(
        ICodeTextSnapshot snapshot,
        IEnumerable<CodeDiagnosticAdornment> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var grouped = new Dictionary<int, List<CodeDiagnosticAdornment>>();
        int count = 0;
        foreach (CodeDiagnosticAdornment diagnostic in diagnostics)
        {
            count++;

            // A diagnostic can span lines; it is indexed under every line it
            // touches so a visible-range query finds it even when its start is
            // scrolled off the top.
            int first = snapshot.GetLineFromPosition(Math.Clamp(diagnostic.Start, 0, snapshot.Length));
            int last = snapshot.GetLineFromPosition(Math.Clamp(diagnostic.End, 0, snapshot.Length));
            for (int line = first; line <= last; line++)
            {
                if (!grouped.TryGetValue(line, out List<CodeDiagnosticAdornment>? bucket))
                    grouped[line] = bucket = [];
                bucket.Add(diagnostic);
            }
        }

        var byLine = new Dictionary<int, CodeDiagnosticAdornment[]>(grouped.Count);
        foreach ((int line, List<CodeDiagnosticAdornment> bucket) in grouped)
        {
            bucket.Sort(static (left, right) => left.Start.CompareTo(right.Start));
            byLine[line] = [.. bucket];
        }

        return new CodeDiagnosticSet(snapshot, byLine, count);
    }

    public ReadOnlySpan<CodeDiagnosticAdornment> GetLineDiagnostics(int line) =>
        _byLine.TryGetValue(line, out CodeDiagnosticAdornment[]? bucket) ? bucket : None;

    /// <summary>The most severe diagnostic on a line, for the gutter marker.</summary>
    public CodeDiagnosticSeverity GetLineSeverity(int line)
    {
        CodeDiagnosticSeverity worst = CodeDiagnosticSeverity.Hidden;
        foreach (CodeDiagnosticAdornment diagnostic in GetLineDiagnostics(line))
        {
            if (diagnostic.Severity > worst)
                worst = diagnostic.Severity;
        }

        return worst;
    }
}
