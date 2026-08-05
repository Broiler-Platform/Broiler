using System;

namespace Broiler.UI.CodeEditor;

/// <summary>
/// The control-facing classification vocabulary. It is deliberately
/// language-neutral: the editor maps these kinds to theme tokens and must never
/// learn C# token kinds, or a second language would require changing the
/// control.
/// </summary>
public enum CodeClassificationKind : byte
{
    /// <summary>Default foreground. Never emitted as a span.</summary>
    None = 0,
    Comment,
    DocumentationComment,
    Keyword,
    ControlKeyword,
    PreprocessorKeyword,
    PreprocessorText,
    StringLiteral,
    EscapeSequence,
    CharacterLiteral,
    NumericLiteral,
    Operator,
    Punctuation,
}

/// <summary>
/// One classified range. <paramref name="Start"/> is relative to the start of
/// its line, not to the document: line-relative offsets survive an edit on
/// another line unchanged, so unaffected lines are reused by reference instead
/// of being rewritten with shifted positions.
/// </summary>
public readonly record struct CodeClassificationSpan(
    int Start,
    int Length,
    CodeClassificationKind Kind);

/// <summary>
/// Classifications for one exact snapshot.
///
/// This is an abstract class rather than an interface so
/// <see cref="GetLineSpans"/> can return a span over the producer's own storage
/// — the control reads spans on every paint of every visible line, and an
/// allocating accessor there would undo the point of virtualizing.
/// </summary>
public abstract class CodeClassificationResult
{
    /// <summary>
    /// The exact snapshot these classifications describe. The control compares
    /// this by reference before painting; a version number alone is not enough,
    /// because it is not comparable across documents.
    /// </summary>
    public abstract ICodeTextSnapshot Snapshot { get; }

    public abstract int LineCount { get; }

    /// <summary>
    /// Spans for one line, with line-relative offsets, in ascending order and
    /// non-overlapping. Returning an empty span means "paint this line in the
    /// default foreground", which is also what identifiers get.
    /// </summary>
    public abstract ReadOnlySpan<CodeClassificationSpan> GetLineSpans(int line);

    /// <summary>An empty result, used before the first classification lands.</summary>
    public static CodeClassificationResult Empty(ICodeTextSnapshot snapshot) =>
        new EmptyResult(snapshot);

    private sealed class EmptyResult(ICodeTextSnapshot snapshot) : CodeClassificationResult
    {
        public override ICodeTextSnapshot Snapshot { get; } = snapshot;

        public override int LineCount => Snapshot.LineCount;

        public override ReadOnlySpan<CodeClassificationSpan> GetLineSpans(int line) => default;
    }
}

/// <summary>
/// Produces classifications for a snapshot. Implementations run off the UI
/// thread and must observe cancellation: a superseded run keeps executing until
/// it next checks its token, so two runs overlap routinely and an implementation
/// holding cross-call state corrupts both.
/// </summary>
public interface ICodeClassifier
{
    CodeClassificationResult Classify(
        ICodeTextSnapshot snapshot,
        CodeClassificationResult? previous,
        CodeTextChange? change,
        System.Threading.CancellationToken cancellationToken);
}

/// <summary>What changed between two snapshots, so a classifier can reuse work.</summary>
public readonly record struct CodeTextChange(int Start, int OldLength, int NewLength)
{
    public int NewEnd => Start + NewLength;

    public int OldEnd => Start + OldLength;
}
