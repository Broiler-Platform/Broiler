using System.Text;

namespace Broiler.Wpt;

/// <summary>
/// Builds a stable, human-readable signature for an exception-bearing WPT failure
/// so that many tests failing for the same reason collapse into one report line
/// (diagnostic #2). The signature is the top non-framework stack frame joined with
/// the normalized exception message, e.g.
/// <c>DomName..ctor — A prefixed name requires a namespace URI</c>. One signature →
/// many tests → one fix.
/// </summary>
internal static class ExceptionSignature
{
    private const int MaxMessageLength = 100;

    // At most this many example test paths are kept per signature — enough to
    // point a maintainer at a concrete reproduction without listing every gated
    // test (one signature → one fix).
    private const int MaxExamplesPerSignature = 3;

    // Stage prefixes the runner prepends to ex.Message at its catch sites. Stripped
    // so the signature keys on the underlying error rather than the pipeline stage
    // (which the failure category already records).
    private static readonly string[] StagePrefixes =
    [
        "Script execution failed: ",
        "Rendering failed: ",
        "Match test failed: ",
        "Failed to read test file: ",
    ];

    /// <summary>
    /// Computes the signature for a failure, or <c>null</c> when there is nothing
    /// usable (neither a parseable application frame nor a message).
    /// </summary>
    public static string? TryCompute(string? message, string? stackTrace)
    {
        var frame = TopNonFrameworkFrame(stackTrace);
        var msg = NormalizeMessage(message);
        return (frame, msg) switch
        {
            (null, null) => null,
            (null, _) => msg,
            (_, null) => frame,
            _ => $"{frame} — {msg}",
        };
    }

    /// <summary>
    /// Groups exception-bearing failures by signature, most frequent first, each
    /// carrying a few example test paths that hit it. Only failures that carry a
    /// stack trace are bucketed (pixel mismatches and other non-exception failures
    /// are excluded); timeouts are excluded too because their synthetic trace is
    /// reported in the dedicated timeout section.
    /// </summary>
    public static IReadOnlyList<ExceptionBucket> Buckets(
        IEnumerable<WptTestResult> failures, int limit)
    {
        var buckets = new Dictionary<string, Accumulator>(StringComparer.Ordinal);
        foreach (var result in failures)
        {
            if (result.StackTrace is null || result.Category == FailureCategory.Timeout)
                continue;
            var signature = TryCompute(result.Message, result.StackTrace);
            if (signature is null)
                continue;
            if (!buckets.TryGetValue(signature, out var accumulator))
            {
                accumulator = new Accumulator();
                buckets[signature] = accumulator;
            }
            accumulator.Count++;
            // Keep the first few distinct paths so the report can name which test
            // to --render to hit this crash.
            if (accumulator.Examples.Count < MaxExamplesPerSignature &&
                !accumulator.Examples.Contains(result.TestPath))
            {
                accumulator.Examples.Add(result.TestPath);
            }
        }

        return buckets
            .OrderByDescending(kv => kv.Value.Count)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(limit)
            .Select(kv => new ExceptionBucket(kv.Key, kv.Value.Count, kv.Value.Examples))
            .ToList();
    }

    private sealed class Accumulator
    {
        public int Count;
        public List<string> Examples { get; } = [];
    }

    private static string? NormalizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        var text = message.Trim();
        foreach (var prefix in StagePrefixes)
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                text = text[prefix.Length..];
                break;
            }
        }

        text = CollapseWhitespace(text);
        if (text.Length == 0)
            return null;
        if (text.Length > MaxMessageLength)
            text = string.Concat(text.AsSpan(0, MaxMessageLength), "…");
        return text;
    }

    /// <summary>
    /// Returns the first stack-trace frame that is not in a framework namespace,
    /// shortened to <c>Type.Method</c> (constructors keep their <c>..ctor</c>
    /// spelling). Returns <c>null</c> when the trace has no parseable application frame.
    /// </summary>
    private static string? TopNonFrameworkFrame(string? stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace))
            return null;

        foreach (var rawLine in stackTrace.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("at ", StringComparison.Ordinal))
                continue;

            var body = line[3..];

            // Drop the " in <file>:line <n>" suffix and the argument list, leaving
            // the fully-qualified method name.
            var inIndex = body.IndexOf(" in ", StringComparison.Ordinal);
            if (inIndex >= 0)
                body = body[..inIndex];
            var parenIndex = body.IndexOf('(');
            if (parenIndex >= 0)
                body = body[..parenIndex];
            body = body.Trim();

            if (body.Length == 0 || IsFrameworkFrame(body))
                continue;

            return ShortenFrame(body);
        }

        return null;
    }

    private static bool IsFrameworkFrame(string fullMethod) =>
        fullMethod.StartsWith("System.", StringComparison.Ordinal) ||
        fullMethod.StartsWith("Microsoft.", StringComparison.Ordinal) ||
        fullMethod.StartsWith("Internal.", StringComparison.Ordinal);

    /// <summary>
    /// Reduces a fully-qualified <c>Namespace.Type.Method</c> to <c>Type.Method</c>,
    /// preserving the constructor spelling (e.g. <c>Namespace.DomName..ctor</c> →
    /// <c>DomName..ctor</c>).
    /// </summary>
    private static string ShortenFrame(string fullMethod)
    {
        string typeFull;
        string method;
        if (fullMethod.EndsWith("..ctor", StringComparison.Ordinal))
        {
            method = ".ctor";
            typeFull = fullMethod[..^6];
        }
        else if (fullMethod.EndsWith("..cctor", StringComparison.Ordinal))
        {
            method = ".cctor";
            typeFull = fullMethod[..^7];
        }
        else
        {
            var lastDot = fullMethod.LastIndexOf('.');
            if (lastDot < 0)
                return fullMethod;
            method = fullMethod[(lastDot + 1)..];
            typeFull = fullMethod[..lastDot];
        }

        var typeDot = typeFull.LastIndexOf('.');
        var typeSimple = typeDot >= 0 ? typeFull[(typeDot + 1)..] : typeFull;
        // method already carries its leading dot for (c)ctors, so joining with '.'
        // yields the canonical "Type..ctor" double-dot spelling.
        return $"{typeSimple}.{method}";
    }

    private static string CollapseWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = true;
                continue;
            }
            if (pendingSpace && builder.Length > 0)
                builder.Append(' ');
            pendingSpace = false;
            builder.Append(ch);
        }
        return builder.ToString();
    }
}

/// <summary>
/// One exception-signature group: the <paramref name="Signature"/>, how many
/// failures share it (<paramref name="Count"/>), and a few example test paths that
/// hit it (<paramref name="Examples"/>) so a report can point at a reproduction.
/// </summary>
public sealed record ExceptionBucket(string Signature, int Count, IReadOnlyList<string> Examples);
