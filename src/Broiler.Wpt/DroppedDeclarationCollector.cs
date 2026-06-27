using System.Collections.Concurrent;

namespace Broiler.Wpt;

/// <summary>
/// Thread-safe aggregator for CSS declarations the style engine dropped because
/// their value failed validation. Wired to
/// <see cref="Broiler.CSS.Dom.CssEngineDiagnostics.DeclarationRejected"/> for the
/// duration of a run and surfaced in the triage report.
///
/// <para>
/// A single high-count entry (e.g. <c>text-align: -webkit-right</c>) usually
/// points straight at a missing feature that silently gates many tests — exactly
/// the failure mode that masked a whole css-align cluster in WPT issue #1100.
/// </para>
/// </summary>
internal sealed class DroppedDeclarationCollector
{
    // Bound memory against pathological cardinality (unique calc()/url()/gradient
    // values). Once the table is full, only keep counting already-seen keys.
    private const int MaxDistinct = 5000;
    private const int MaxValueLength = 80;

    private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.Ordinal);

    /// <summary>Records one dropped declaration.</summary>
    public void Record(string property, string value)
    {
        var key = FormatKey(property, value);
        if (_counts.Count >= MaxDistinct && !_counts.ContainsKey(key))
            return;
        _counts.AddOrUpdate(key, 1, static (_, count) => count + 1);
    }

    /// <summary>Total dropped-declaration occurrences recorded.</summary>
    public int TotalDropped => _counts.Values.Sum();

    /// <summary>Distinct "property: value" entries, highest count first.</summary>
    public IReadOnlyList<(string Declaration, int Count)> Top(int limit) =>
        _counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(limit)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();

    /// <summary>
    /// Canonical "property: value" key (property lower-cased, value trimmed and
    /// length-capped so high-cardinality values can't bloat the report).
    /// </summary>
    internal static string FormatKey(string property, string value)
    {
        var prop = property.Trim().ToLowerInvariant();
        var val = value.Trim();
        if (val.Length > MaxValueLength)
            val = string.Concat(val.AsSpan(0, MaxValueLength), "…");
        return $"{prop}: {val}";
    }
}
