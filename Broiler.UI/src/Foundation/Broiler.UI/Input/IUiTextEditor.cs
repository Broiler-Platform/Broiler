using System;

namespace Broiler.UI;

/// <summary>
/// Neutral two-way editor surface used by platform text services such as Android
/// <c>InputConnection</c>, Windows TSF, and browser composition.
///
/// Every read is bounded. An earlier shape returned the whole text with the
/// selection, which is fine for an edit box and ruinous for a source editor: an
/// IME asking for a few characters of context around the caret would
/// materialize a multi-megabyte document on every keystroke. There is
/// deliberately no operation here that returns the entire buffer.
///
/// The compatible shape means a small control answers a bounded query by
/// returning everything it has. Broiler.UI ADR 0022 records the decision.
/// </summary>
public interface IUiTextEditor
{
    /// <summary>Sizes and offsets only, never text.</summary>
    UiTextEditorMetrics GetTextEditorMetrics();

    /// <summary>
    /// Reads at most <paramref name="maxLength"/> characters from
    /// <paramref name="start"/>. Requests are clamped to the document rather
    /// than rejected, so a caller asking for more context than exists gets what
    /// there is.
    /// </summary>
    string GetTextEditorRange(int start, int maxLength);

    bool DeleteSurroundingText(int beforeLength, int afterLength);

    bool SetEditorSelection(int start, int end);

    bool SetComposingRegion(int start, int end);

    bool PerformEditorAction(UiTextEditorAction action);
}

/// <summary>
/// The shape of the edited text, with no content. A password field reports zero
/// length and a collapsed selection rather than reporting its contents.
/// </summary>
public readonly record struct UiTextEditorMetrics(
    int TextLength,
    int SelectionStart,
    int SelectionEnd,
    int ComposingStart = -1,
    int ComposingEnd = -1)
{
    public bool HasComposingRegion => ComposingStart >= 0 && ComposingEnd >= ComposingStart;
}

public enum UiTextEditorAction
{
    None = 0,
    Done,
    Go,
    Next,
    Previous,
    Search,
    Send,
}

/// <summary>
/// Range clamping shared by every <see cref="IUiTextEditor"/> implementation, so
/// they cannot disagree about what an out-of-range request means.
/// </summary>
public static class UiTextEditorRange
{
    /// <summary>
    /// Clamps a request against a document length. Returns the clamped start
    /// and the number of characters that may be read.
    /// </summary>
    public static (int Start, int Length) Clamp(int textLength, int start, int maxLength)
    {
        if (textLength <= 0 || maxLength <= 0)
            return (0, 0);
        int clampedStart = Math.Clamp(start, 0, textLength);
        int length = Math.Min(Math.Max(0, maxLength), textLength - clampedStart);
        return (clampedStart, length);
    }

    /// <summary>Bounded read from a string that is already in memory.</summary>
    public static string Slice(string text, int start, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        (int clampedStart, int length) = Clamp(text.Length, start, maxLength);
        return length == 0 ? string.Empty : text.Substring(clampedStart, length);
    }
}
