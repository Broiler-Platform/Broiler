namespace Broiler.UI;

/// <summary>
/// Bounded, versioned access to a text surface too large to hand over whole.
///
/// A screen reader that receives a ten-megabyte string per query is not usable,
/// and the marshalling alone would exceed any interaction budget. Every read
/// here names a range and gets back at most that range; there is deliberately
/// no operation that returns the entire document.
///
/// Every operation also names the version it was composed against and is
/// rejected when stale, so a client holding a range across an edit gets a
/// refusal it can retry rather than text from a document that has moved.
///
/// Broiler.UI ADR 0022 records the decision. Platform accessibility bridges
/// arrive in Phase 7; this contract is testable without them, and a passing
/// contract test is not a screen-reader support claim.
/// </summary>
public interface IUiVirtualizedTextProvider
{
    /// <summary>Sizes and offsets only — never text.</summary>
    UiTextDocumentMetrics GetTextMetrics();

    /// <summary>
    /// Reads at most <paramref name="maxLength"/> characters starting at
    /// <paramref name="start"/>. A request larger than the implementation is
    /// willing to serve is truncated rather than refused: a client that gets
    /// less context still works, and one that gets an error does not.
    /// </summary>
    UiTextRangeResult GetTextRange(int start, int maxLength, int expectedVersion);

    /// <summary>Line extent without materializing the line.</summary>
    UiTextLineInfo GetLineInfo(int line, int expectedVersion);

    bool TrySetSelection(int start, int end, int expectedVersion);

    bool TryReplaceRange(int start, int length, string text, int expectedVersion);
}

/// <summary>
/// The shape of the document, with no content. <paramref name="Version"/> is
/// what every other call is checked against.
/// </summary>
public readonly record struct UiTextDocumentMetrics(
    int Version,
    int Length,
    int LineCount,
    int SelectionStart,
    int SelectionEnd,
    int CompositionStart,
    int CompositionEnd,
    bool IsEditable)
{
    public bool HasComposition => CompositionStart >= 0 && CompositionEnd >= CompositionStart;
}

/// <summary>
/// A bounded read. <paramref name="Start"/> and <paramref name="Text"/> describe
/// what was actually returned, which may be shorter than what was asked for.
/// </summary>
public readonly record struct UiTextRangeResult(bool IsValid, int Start, string Text)
{
    public static UiTextRangeResult Stale => new(false, 0, string.Empty);

    public int End => Start + Text.Length;
}

public readonly record struct UiTextLineInfo(bool IsValid, int Line, int Start, int Length)
{
    public static UiTextLineInfo Stale => new(false, 0, 0, 0);

    public int End => Start + Length;
}
