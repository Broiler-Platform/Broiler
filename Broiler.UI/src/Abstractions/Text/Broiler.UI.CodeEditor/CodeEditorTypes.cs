using System;

namespace Broiler.UI.CodeEditor;

/// <summary>An anchor/focus pair over the document. Focus is where the caret is.</summary>
public readonly record struct CodeSelection(int Anchor, int Focus)
{
    public int Start => Math.Min(Anchor, Focus);

    public int End => Math.Max(Anchor, Focus);

    public int Length => End - Start;

    public bool IsEmpty => Anchor == Focus;

    public static CodeSelection Caret(int position) => new(position, position);
}

/// <summary>The visible window, in lines and pixels, that the renderer paints.</summary>
public readonly record struct CodeViewport(int FirstVisibleLine, int VisibleLineCount, double HorizontalOffset)
{
    public int LastVisibleLine => FirstVisibleLine + Math.Max(0, VisibleLineCount - 1);
}

public enum CodeCaretMovement
{
    Left,
    Right,
    Up,
    Down,
    LineStart,
    LineEnd,
    DocumentStart,
    DocumentEnd,
    PageUp,
    PageDown,
    WordLeft,
    WordRight,
}

/// <summary>
/// How the editor reports what it can currently do. A host without the complete
/// semantic stack shows classification-only mode rather than pretending the
/// absence of diagnostics means the code is correct.
/// </summary>
public enum CodeAnalysisMode
{
    /// <summary>Syntax colouring only; no semantic diagnostics are available.</summary>
    ClassificationOnly = 0,

    /// <summary>Diagnostics come from the last authoritative build.</summary>
    BuildDiagnostics,

    /// <summary>A live semantic service is attached.</summary>
    LiveSemantic,
}

public sealed class CodeSelectionChangedEventArgs(CodeSelection selection) : EventArgs
{
    public CodeSelection Selection { get; } = selection;
}

public sealed class CodeSnapshotChangedEventArgs(ICodeTextSnapshot snapshot) : EventArgs
{
    public ICodeTextSnapshot Snapshot { get; } = snapshot;
}

public sealed class CodeViewportChangedEventArgs(CodeViewport viewport) : EventArgs
{
    public CodeViewport Viewport { get; } = viewport;
}

/// <summary>
/// Raised when an intent is refused, so a host can surface why rather than
/// leaving the user with a keystroke that silently did nothing.
/// </summary>
public sealed class CodeEditRejectedEventArgs(CodeEditIntent intent, CodeEditRejection reason) : EventArgs
{
    public CodeEditIntent Intent { get; } = intent;

    public CodeEditRejection Reason { get; } = reason;
}

/// <summary>Tab width and whether Tab inserts spaces.</summary>
public readonly record struct CodeIndentPolicy(int TabSize, bool InsertSpaces)
{
    public static CodeIndentPolicy Default => new(4, true);

    public string GetIndentUnit() => InsertSpaces ? new string(' ', Math.Max(1, TabSize)) : "\t";
}
