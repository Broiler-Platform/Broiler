namespace Broiler.UI.RichEdit;

/// <summary>
/// The first-release RichEdit command set (ADR 0015). Applications drive the
/// editor through these declarative commands and query
/// <see cref="UiRichEdit.GetCommandState"/> to reflect toolbar state, without
/// inspecting document internals.
/// </summary>
public enum RichEditCommand
{
    None = 0,

    // Edit
    Undo,
    Redo,
    Cut,
    Copy,
    Paste,
    SelectAll,

    // Insertion
    InsertText,
    InsertParagraphBreak,
    InsertLineBreak,

    // Inline format
    Bold,
    Italic,
    Underline,
    Strikethrough,
    SetForeground,
    SetBackground,
    SetFontFamily,
    SetFontSize,
    SetFont,
    ClearFormatting,

    // Paragraph format
    AlignLeft,
    AlignCenter,
    AlignRight,
    BulletList,
    NumberedList,
    Indent,
    Outdent,
}
