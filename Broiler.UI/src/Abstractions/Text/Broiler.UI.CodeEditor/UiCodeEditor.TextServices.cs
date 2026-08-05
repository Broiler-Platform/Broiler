using System;

namespace Broiler.UI.CodeEditor;

/// <summary>
/// The editor's two bounded text surfaces: the platform text-service contract
/// used by IMEs, and the virtualized accessibility contract.
///
/// Neither can return the whole document, and both clamp rather than throw. An
/// IME that asks for more context than exists still composes correctly; one
/// that gets an exception does not.
/// </summary>
public abstract partial class UiCodeEditor
{
    /// <summary>
    /// How much text either contract will hand over in one call. An IME needs
    /// tens of characters and an accessibility client reads a screenful; a
    /// caller wanting more asks again with a new start.
    /// </summary>
    public const int MaxBoundedReadLength = 8192;

    // ----- IUiTextEditor: the platform text-service surface. -----

    public UiTextEditorMetrics GetTextEditorMetrics()
    {
        ICodeTextSnapshot snapshot = Snapshot;
        return new UiTextEditorMetrics(
            snapshot.Length,
            Selection.Start,
            Selection.End,
            HasComposition ? CompositionStart : -1,
            HasComposition ? CompositionEnd : -1);
    }

    public string GetTextEditorRange(int start, int maxLength)
    {
        ICodeTextSnapshot snapshot = Snapshot;
        (int clampedStart, int length) = UiTextEditorRange.Clamp(
            snapshot.Length, start, Math.Min(maxLength, MaxBoundedReadLength));
        return length == 0 ? string.Empty : snapshot.GetText(clampedStart, length);
    }

    public bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
        if (IsReadOnly || !IsEnabled)
            return false;

        ICodeTextSnapshot snapshot = Snapshot;
        int start = Math.Max(0, Selection.Start - Math.Max(0, beforeLength));
        int end = Math.Min(snapshot.Length, Selection.End + Math.Max(0, afterLength));
        return end > start && Replace(start, end - start, string.Empty, "delete-surrounding");
    }

    public bool SetEditorSelection(int start, int end)
    {
        if (!IsEnabled)
            return false;
        Selection = new CodeSelection(start, end);
        return true;
    }

    public bool SetComposingRegion(int start, int end)
    {
        if (IsReadOnly || !IsEnabled)
            return false;

        int length = Snapshot.Length;
        int clampedStart = Math.Clamp(start, 0, length);
        int clampedEnd = Math.Clamp(end, clampedStart, length);
        SetComposition(clampedStart, clampedEnd);
        return true;
    }

    public bool PerformEditorAction(UiTextEditorAction action)
    {
        if (IsReadOnly || !IsEnabled)
            return false;

        // A source editor has no "done" or "next field"; the only action with a
        // meaning here is inserting a line.
        return action switch
        {
            UiTextEditorAction.Done or UiTextEditorAction.Go or UiTextEditorAction.Send => InsertNewLine(),
            _ => false,
        };
    }

    // ----- IUiVirtualizedTextProvider: the accessibility surface. -----

    public UiTextDocumentMetrics GetTextMetrics()
    {
        ICodeTextSnapshot snapshot = Snapshot;
        return new UiTextDocumentMetrics(
            snapshot.Version,
            snapshot.Length,
            snapshot.LineCount,
            Selection.Start,
            Selection.End,
            HasComposition ? CompositionStart : -1,
            HasComposition ? CompositionEnd : -1,
            !IsReadOnly && IsEnabled);
    }

    public UiTextRangeResult GetTextRange(int start, int maxLength, int expectedVersion)
    {
        ICodeTextSnapshot snapshot = Snapshot;
        if (!IsCurrent(snapshot, expectedVersion))
            return UiTextRangeResult.Stale;

        (int clampedStart, int length) = UiTextEditorRange.Clamp(
            snapshot.Length, start, Math.Min(maxLength, MaxBoundedReadLength));
        return new UiTextRangeResult(
            true, clampedStart, length == 0 ? string.Empty : snapshot.GetText(clampedStart, length));
    }

    public UiTextLineInfo GetLineInfo(int line, int expectedVersion)
    {
        ICodeTextSnapshot snapshot = Snapshot;
        if (!IsCurrent(snapshot, expectedVersion))
            return UiTextLineInfo.Stale;
        if (line < 0 || line >= snapshot.LineCount)
            return UiTextLineInfo.Stale;

        return new UiTextLineInfo(true, line, snapshot.GetLineStart(line), snapshot.GetLineLength(line));
    }

    public bool TrySetSelection(int start, int end, int expectedVersion)
    {
        if (!IsCurrent(Snapshot, expectedVersion) || !IsEnabled)
            return false;
        Selection = new CodeSelection(start, end);
        return true;
    }

    public bool TryReplaceRange(int start, int length, string text, int expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!IsCurrent(Snapshot, expectedVersion))
            return false;
        return Replace(start, length, text, "accessibility-edit");
    }

    /// <summary>
    /// A negative expected version means "whatever is current" — the first call
    /// a client makes has nothing to compare against. Any other value must match
    /// exactly, so a client holding a range across an edit is refused rather
    /// than served text from a document that has moved.
    /// </summary>
    private static bool IsCurrent(ICodeTextSnapshot snapshot, int expectedVersion) =>
        expectedVersion < 0 || expectedVersion == snapshot.Version;
}
